using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Newtonsoft.Json;
using ResourceType = Amazon.EC2.ResourceType;
using Tag = Amazon.EC2.Model.Tag;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ce_pipeline_lambda_function
{
    public class Function
    {
        private static IAmazonS3 _s3Client;

        public Function()
        {
            _s3Client = new AmazonS3Client(RegionEndpoint.EUCentral1);
        }

        public async Task<CEPipelineInput> FunctionHandler(CEPipelineInput input, ILambdaContext context)
        {
            if (input == null)
            {
                throw new Exception("Unable to extract requestParameters from Lambda input parameter");
            }

            var output = await ProcessStep(input.StepName, input);

            return output;
        }

        private static async Task<CEPipelineInput> ProcessStep(string stepName, CEPipelineInput input)
        {
            Console.WriteLine($"************************ Running Task => {stepName} *****************************");


            switch (stepName)
            {
                case "LaunchEC2Instance":

                    await LaunchEC2Instance(input);

                    return new CEPipelineInput();

                case "DownloadFromS3":

                    await DownloadFromS3(input);

                    return new CEPipelineInput();

                case "CreateAMI":
                    
                    await CreateAMI(input);

                    return new CEPipelineInput();

            }

            return null;
        }

        private static async Task LaunchEC2Instance(CEPipelineInput input)
        {
            const string installer = "ProphetWorkerX64.zip";
            input.UploadFile = installer;

            var nodeTag = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ec2Client = new AmazonEC2Client(RegionEndpoint.GetBySystemName("eu-central-1"));

            var tagSpec = new List<TagSpecification>
            {
                new TagSpecification {Tags = new List<Tag> {new Tag("Name", $"kwex_ce_ami_creator_{nodeTag}")}, ResourceType = ResourceType.Instance}
            };

            var instanceProfile = new IamInstanceProfileSpecification
            {
                Arn = "arn:aws:iam::913102450908:instance-profile/EC2_SSMManagedinstance_policy"
            };

            var userData = @"
                <powershell>
                    $instanceId = Get-EC2InstanceMetadata -Category InstanceId
                    Send-SFNTaskSuccess -TaskToken '" + input.TaskToken + @"' -Output ""{""""InstanceId"""": """"$instanceId"""", """"BucketName"""": """"" 
                           + input.BucketName + @""""", """"UploadFile"""": """"" + input.UploadFile + @"""""}""
                </powershell>
              ";
            
            var launchRequest = new RunInstancesRequest
            {
                ImageId = "ami-0af90660b3b96f3ff",
                IamInstanceProfile = instanceProfile,
                InstanceType = "t3.medium",
                MinCount = 1,
                MaxCount = 1,
                SecurityGroupIds = { "sg-0ffb6443954906a79" },
                SubnetId = "subnet-038544cab8b1355cc",
                TagSpecifications = tagSpec,
                KeyName = "kwex_keypair",
                UserData = Convert.ToBase64String(Encoding.UTF8.GetBytes(userData))
            };

            Console.WriteLine($"LaunchRequest: [{JsonConvert.SerializeObject(launchRequest)}]");

            var launchResponse = await ec2Client.RunInstancesAsync(launchRequest);

            var instances = launchResponse.Reservation.Instances;

            input.InstanceId = instances[0].InstanceId;

            Console.WriteLine($"Output from LaunchEC2Instance: [{JsonConvert.SerializeObject(input)}]");
        }

        private static async Task DownloadFromS3(CEPipelineInput input)
        {
            Console.WriteLine("Inside DownloadAndExtractInstallerFromS3 method");

            Console.WriteLine($"Input: [{JsonConvert.SerializeObject(input)}]");

            var ssmClient = new AmazonSimpleSystemsManagementClient();
            var instanceId = !string.IsNullOrEmpty(input.InstanceId) ? input.InstanceId.Trim() : string.Empty;

            var sendCommandRequest = new SendCommandRequest
            {
                DocumentName = "AWS-RunPowerShellScript",
                InstanceIds = new List<string> {instanceId},
                Parameters = new Dictionary<string, List<string>>
                {
                    { "commands", new List<string>
                    {
                        "New-Item 'C:\\Installers' -ItemType Directory",
                        $"Read-S3Object -BucketName kwex-ce-installers  -Key {input.UploadFile}  -File C:\\Installers\\{input.UploadFile}",
                        $"Send-SFNTaskSuccess -TaskToken '{input.TaskToken}' -Output '{{\"InstanceId\": \"{instanceId}\"}}'"
                    }}
                }
            };

            Console.WriteLine($"sendCommandRequest: [{JsonConvert.SerializeObject(sendCommandRequest)}]");

            var sendCommandResponse = ssmClient.SendCommandAsync(sendCommandRequest).Result;
            // var sendCommandResponse = await ssmClient.SendCommandAsync(sendCommandRequest);

            Console.WriteLine($"Response: [{JsonConvert.SerializeObject(sendCommandResponse)}]");
        }

        private static async Task CreateAMI(CEPipelineInput input)
        {
            Console.WriteLine("Inside CreateAMI method");

            Console.WriteLine($"Input: [{JsonConvert.SerializeObject(input)}]");

            var ec2Client = new AmazonEC2Client(RegionEndpoint.GetBySystemName("eu-central-1"));

            var amiTag = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var imageRequest = new CreateImageRequest
            {
                BlockDeviceMappings = new List<BlockDeviceMapping>
                {
                    new BlockDeviceMapping
                    {
                        DeviceName = "/dev/sdh",
                        Ebs = new EbsBlockDevice {VolumeSize = 100}
                    }
                },
                Description = "Kwex Calc Engine Auto AMI",
                InstanceId = input.InstanceId,
                Name = $"kwex-ce-ami-{amiTag}",
                NoReboot = true
            };

            var response = await ec2Client.CreateImageAsync(imageRequest);

            Console.WriteLine($"Image Id: [{response.ImageId}]");
        }
    }
}
