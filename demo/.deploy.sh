#!/bin/bash

#############################################################################
# TRAVIS environment variables:                                             #
#   AWS_ACCOUNT_test                                                        #
#   AWS_ACCOUNT_staging                                                     #
#   AWS_ACCOUNT_prod                                                        #
#   AWS_ACCESS_KEY_ID                                                       #
#   AWS_SECRET_ACCESS_KEY                                                   #
#   AWS_DEPLOY_ROLE (use the same name for all the accounts)                #
#   AWS_DEFAULT_REGION                                                      #
#############################################################################

set -e

#############################################################################
# BASH arguments                                                            #
#############################################################################

# test, staging or prod
ENVIRONMENT=${1:-test}
# the csproj file name
CSPROJ_FILENAME=${2:-MyProject}
# assembly filename as mentioned at the .csproj
ASSEMBLY_FILENAME=${3:-MyDemoAssemblyName}
# a tag used to identify resources on AWS
TAG_CODE=${4:-MyAWSTagCode}


# STEP 1
######################################################################################################
echo "Setting AWS Credentials ########################################################################"      

AWS_ACCOUNT=$(case $ENVIRONMENT in test) echo $AWS_ACCOUNT_test ;; staging) echo $AWS_ACCOUNT_staging ;; prod) echo $AWS_ACCOUNT_prod ;; esac)
AWS_ROLE=$(echo arn:aws:iam::$AWS_ACCOUNT:role/$AWS_DEPLOY_ROLE)

echo "using $AWS_ROLE role for account $AWS_ACCOUNT"
# create AWS .config credentials file 
aws configure set default.aws_access_key_id $AWS_ACCESS_KEY_ID
aws configure set default.aws_secret_access_key $AWS_SECRET_ACCESS_KEY
aws configure set default.region $AWS_DEFAULT_REGION
aws configure set profile.deploy.role_arn $AWS_ROLE
aws configure set profile.deploy.source_profile default

# STEP 2
######################################################################################################
echo "Building artifact ##############################################################################"            

# csproj files
CSPROJ_FULLPATH=$(echo $TRAVIS_BUILD_DIR/src/$CSPROJ_FILENAME.csproj)

# artifacts local output location
ARTIFACT_PATH=$(echo $TRAVIS_BUILD_DIR/artifacts/)
ARTIFACT_FILENAME=$(echo $CSPROJ_FILENAME-$TRAVIS_BUILD_NUMBER.zip)
ARTIFACT_FULLPATH=$(echo $ARTIFACT_PATH/$ARTIFACT_FILENAME)

# define code constants
sed -i -e "s/RELEASE/${ENVIRONMENT^^}/g" $CSPROJ_FULLPATH
# publish dotnet code
dotnet publish $TRAVIS_BUILD_DIR/src -o $ARTIFACT_PATH --framework netcoreapp2.1 --runtime linux-x64 -c Release
# package code
zip -j $ARTIFACT_FULLPATH $ARTIFACT_PATH/* 

# results example: ./artifacts/prod-34.zip
###################################################

# STEP 3
######################################################################################################
echo "Sending artifact to AWS S3 #####################################################################"     

# artifact location on AWS S3
ARTIFACT_S3_BUCKET=$(echo cf4dotnet-$AWS_ACCOUNT)
ARTIFACT_S3_KEY=$(echo nway.map.component.login.api/$ARTIFACT_FILENAME)
ARTIFACT_S3_URI=$(echo s3://$ARTIFACT_S3_BUCKET/$ARTIFACT_S3_KEY)

# check for S3 bucket
if  aws --profile deploy s3api list-buckets --output text | grep $ARTIFACT_S3_BUCKET; then
    echo "Bucket found"
else
    echo "Creating the artifacts bucket"
    aws --profile deploy s3api create-bucket --bucket $ARTIFACT_S3_BUCKET --region $AWS_DEFAULT_REGION
fi

# upload code to S3 bucket
echo "copying $ARTIFACT_FULLPATH to $ARTIFACT_S3_URI"
aws s3 --profile deploy cp $ARTIFACT_FULLPATH $ARTIFACT_S3_URI

# s3://$ARTIFACT_S3_BUCKET/$ARTIFACT_S3_KEY zip file is on  S3
######################################################################################################

# STEP 4
######################################################################################################
echo "Building AWS Cloudformation templates ##########################################################"  

# cloudformation injection project
CF4DOTNET_SOURCE_DLL=$(echo $ARTIFACT_PATH/$ASSEMBLY_FILENAME.dll)
CF_FUNCTIONS_PREFIX=$(echo $TAG_CODE | sed 's/[^a-zA-Z0-9]/-/g')

$TRAVIS_BUILD_DIR/tools/dotnet-cf4dotnet api $CF4DOTNET_SOURCE_DLL -b $TRAVIS_BUILD_NUMBER -e $ENVIRONMENT -p $CF_FUNCTIONS_PREFIX

# $TRAVIS_BUILD_DIR/sam-base.yml and $TRAVIS_BUILD_DIR/sam-$ENVIRONMENT.yml created.
######################################################################################################

# STEP 5
######################################################################################################
echo "deploying templates to AWS #####################################################################" 

CF_BASE_TEMPLATE=$(echo $TRAVIS_BUILD_DIR/sam-base.yml)
CF_ENVIRONMENT_TEMPLATE=$(echo $TRAVIS_BUILD_DIR/sam-$ENVIRONMENT.yml)

CF_BASE_STACKNAME=$(echo $TAG_CODE-base | sed 's/[^a-zA-Z0-9]/-/g')
CF_ENVIRONMENT_STACKNAME=$(echo $TAG_CODE-$ENVIRONMENT | sed 's/[^a-zA-Z0-9]/-/g')

# deploy base template
echo "deploying base template ..."
aws cloudformation deploy --profile deploy --template-file $CF_BASE_TEMPLATE --stack-name $CF_BASE_STACKNAME --parameter-overrides ArtifactS3Bucket=$ARTIFACT_S3_BUCKET  ArtifactS3BucketKey=$ARTIFACT_S3_KEY --tags appcode=$TAG_CODE --no-fail-on-empty-changeset --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM

# deploy environment template
echo "deploying $ENVIRONMENT template ..."
aws cloudformation deploy --profile deploy --template-file $CF_ENVIRONMENT_TEMPLATE --stack-name $CF_ENVIRONMENT_STACKNAME --tags appcode=$TAG_CODE --no-fail-on-empty-changeset --capabilities CAPABILITY_IAM CAPABILITY_NAMED_IAM

# results: your code is on AWS!
############################################################################