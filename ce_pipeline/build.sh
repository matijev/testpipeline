if [[ $1 != "--skip-build" ]] ; then  
    echo "*********** Building Lambda function *********************"

	cd ce-pipeline-lambda-function/src/
	dotnet lambda package -c Release -o ../../publish/ce_pipeline_lambda.zip
	cd ../../
fi

echo " "
echo " "
echo "*********** Publishing Lambda to AWS *********************"

cd terraform/
# terraform init
terraform apply --auto-approve