provider "aws" {
  region  = "eu-central-1"
  profile = "default"
}

locals {
  subnet_1       = "subnet-038544cab8b1355cc"
  subnet_2       = "subnet-0532aab4ffc7c1bea"
  subnet_3       = "subnet-09ec09afcf39c5712"
  security_group = "sg-07814b35b34dedf80"

  lambda_zip_location = "../publish/ce_pipeline_lambda.zip"
}


resource "aws_iam_role_policy" "cepipeline_sfn_policy" {
  name = "cepipeline_sfn_policy"
  role = aws_iam_role.cepipeline_sfn_role.id

  policy = <<-EOF
  {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "logs:CreateLogGroup",
                "logs:CreateLogStream",
                "logs:PutLogEvents",
                "lambda:InvokeAsync",
                "lambda:InvokeFunction"
                ],
                "Resource": "arn:aws:lambda:*:*:*:*"
        }
    ]
  }
  EOF
}

resource "aws_iam_role" "cepipeline_sfn_role" {
  name = "cepipeline_sfn_role"

  assume_role_policy = <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Action": "sts:AssumeRole",
      "Principal": {
        "Service": "states.amazonaws.com"
      },
      "Effect": "Allow",
      "Sid": ""
    }
  ]
}
EOF
}


resource "aws_iam_role_policy" "cepipeline_lambda_policy" {
  name = "cepipeline_lambda_policy"
  role = aws_iam_role.cepipeline_lambda_role.id

  policy = <<-EOF
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": [
          "s3:*"
        ],
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": [
            "logs:CreateLogGroup",
            "logs:CreateLogStream",
            "logs:PutLogEvents"
        ],
        "Resource": "*"
      },
      {
        "Action": "ec2:*",
        "Effect": "Allow",
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": "elasticloadbalancing:*",
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": "cloudwatch:*",
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": "autoscaling:*",
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": "iam:CreateServiceLinkedRole",
        "Resource": "*",
        "Condition": {
            "StringEquals": {
                "iam:AWSServiceName": [
                    "autoscaling.amazonaws.com",
                    "ec2scheduled.amazonaws.com",
                    "elasticloadbalancing.amazonaws.com",
                    "spot.amazonaws.com",
                    "spotfleet.amazonaws.com",
                    "transitgateway.amazonaws.com"
                ]
            }
        }
      },
      {
        "Effect": "Allow",
        "Action": [
            "ssm:SendCommand",
            "ssm:DescribeAssociation",
            "ssm:GetDeployablePatchSnapshotForInstance",
            "ssm:GetDocument",
            "ssm:DescribeDocument",
            "ssm:GetManifest",
            "ssm:GetParameter",
            "ssm:GetParameters",
            "ssm:ListAssociations",
            "ssm:ListInstanceAssociations",
            "ssm:PutInventory",
            "ssm:PutComplianceItems",
            "ssm:PutConfigurePackageResult",
            "ssm:UpdateAssociationStatus",
            "ssm:UpdateInstanceAssociationStatus",
            "ssm:UpdateInstanceInformation"
        ],
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": [
            "ssmmessages:CreateControlChannel",
            "ssmmessages:CreateDataChannel",
            "ssmmessages:OpenControlChannel",
            "ssmmessages:OpenDataChannel"
        ],
        "Resource": "*"
      },
      {
        "Effect": "Allow",
        "Action": [
            "ec2messages:AcknowledgeMessage",
            "ec2messages:DeleteMessage",
            "ec2messages:FailMessage",
            "ec2messages:GetEndpoint",
            "ec2messages:GetMessages",
            "ec2messages:SendReply"
        ],
        "Resource": "*"
      },
      {
          "Effect": "Allow",
          "Action": "iam:PassRole",
          "Resource": "arn:aws:iam::913102450908:role/EC2_SSMManagedinstance_policy"
      }
    ]
  }
  EOF
}

resource "aws_iam_role" "cepipeline_lambda_role" {
  name = "cepipeline_lambda_role"

  assume_role_policy = <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Action": "sts:AssumeRole",
      "Principal": {
        "Service": "lambda.amazonaws.com"
      },
      "Effect": "Allow",
      "Sid": ""
    }
  ]
}
EOF
}

resource "aws_lambda_function" "cepipeline_lambda" {
  filename         = local.lambda_zip_location
  function_name    = "cepipeline_lambda"
  role             = aws_iam_role.cepipeline_lambda_role.arn
  handler          = "ce_pipeline_lambda_function::ce_pipeline_lambda_function.Function::FunctionHandler"
  source_code_hash = filebase64sha256(local.lambda_zip_location)
  runtime          = "dotnetcore3.1"
  timeout          = 30

  # vpc_config {
  #   subnet_ids         = [local.subnet_1, local.subnet_2, local.subnet_3]
  #   security_group_ids = [local.security_group]
  # }


  environment {
    variables = {
      foo = "bar"
    }
  }
}


resource "aws_sfn_state_machine" "cepipeline_sfn" {
  name     = "cepipeline_sfn"
  role_arn = aws_iam_role.cepipeline_sfn_role.arn

  definition = <<EOF
{
  "Comment": "CE Pipeline State Machine",
  "StartAt": "LaunchEC2Instance",
  "States": {
    "LaunchEC2Instance": {
      "Type": "Task",
      "Resource": "arn:aws:states:::lambda:invoke.waitForTaskToken",
      "Parameters": {
        "FunctionName": "cepipeline_lambda",
        "Payload":{  
          "StepName.$" : "$$.State.Name",
          "BucketName.$" : "$.detail.requestParameters.bucketName",
          "UploadFile.$" : "$.detail.requestParameters.key",
          "TaskToken.$" : "$$.Task.Token"
        }
      },
      "Retry": [
        {
          "ErrorEquals": ["States.TaskFailed"],
          "IntervalSeconds": 10,
          "MaxAttempts": 2,
          "BackoffRate": 2.0
        }
      ],
      "Next": "DownloadFromS3"
    },
    "DownloadFromS3": {
      "Type": "Task",
      "Resource": "arn:aws:states:::lambda:invoke.waitForTaskToken",
      "Parameters": {
        "FunctionName": "cepipeline_lambda",
        "Payload":{  
          "StepName.$" : "$$.State.Name",
          "BucketName.$" : "$.BucketName",
          "UploadFile.$" : "$.UploadFile",
          "InstanceId.$" : "$.InstanceId",
          "TaskToken.$" : "$$.Task.Token"
        }
      },
      "Retry": [
        {
          "ErrorEquals": ["States.TaskFailed"],
          "IntervalSeconds": 10,
          "MaxAttempts": 2,
          "BackoffRate": 2.0
        }
      ],
      "Next": "CreateAMI"
    },
    "CreateAMI": {
      "Type": "Task",
      "Resource": "${aws_lambda_function.cepipeline_lambda.arn}",
      "Parameters": {
        "StepName.$" : "$$.State.Name",
        "InstanceId.$" : "$.InstanceId"
      },
      "End": true
    }
  }
}
EOF
}
