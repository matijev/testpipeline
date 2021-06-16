using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ce_pipeline_lambda_function;

namespace ce_pipeline_lambda_function_test
{
    [TestClass]
    public class CEPipelineTests
    {
        [TestMethod]
        public void DownloadFileFromS3Test()
        {
            var lambdaFxn = new Function();

            // var input = GetData("data/s3Event_input.json");

            var cePipelineInput = new CEPipelineInput
            {
                BucketName = "kwex-ce-installers",
                StepName = "DownloadFromS3",
                UploadFile = "ProphetWorkerX64.zip",
                InstanceId = "i-00003f5040362891b",
                TaskToken = "AAAAKgAAAAIAAAAAAAAAAbbSyCQecieAJi8EYtM/YaA0n7M9sNBjfr30uqFomk7t3Xo6i4JDSIDiR2wc5jwYsk/INV3ObpUuZYYH7MwflMHhSTD45Lv0qsFl+hukA65jdbwqThL8gimoP9gd8mCdHXY5gh4J/IjSio+N8ryq/qBtc4CPlScoNN/xuGE4Zc43MIzEJF295rLk4cKq9evQwEQHBR2szfAtvqrIRMJMJMALWyaSAK0h7gQNvquZjw3ECAwDWTs/GWC5wWniLEJd9wqnDXob+BR/rnS++rRbYhj5Bo0OiSoDul3TXDQH/JYqqdHbbUcuxfYipAG269V2XxEKzLGJKCo+bFeje/Z80PUcNLT6R3fo0/JhpvLqfWsoQsFfUUtetHp3jJ4a8m9aX54AI60kEo0FEyVBj3X1Oc9zUNrUvl0gwCKDNUa09cvSU6wQ+aKDfX9UD8MUnfRzoGHWzKuKeX8HuXKM1+4avXTeVZWuSpP1MqIOH3hP69knJdeJ3cEdoutL6zkjuP/iJFDeRSOrNBIkRiyd8mwWllrNK3y+nlx/eR36LzNl2mu4PUdUSnqkQGtlEmwN/Z+yZQNRG+88D1DyGejhVgGImJxV8ne1j2wAP3Yh0hpd8njH1zP4dDxrgdCqOaqYvP6usA=="
            };

            var lambdaOutput = lambdaFxn.FunctionHandler(cePipelineInput, null);

            // Assert.AreEqual(lambdaOutput.Result.BucketName, $"{cePipelineInput.BucketName}-extract");
        }

        private static JObject GetData(string file)
        {
            var s = File.ReadAllText(file);

            return JsonConvert.DeserializeObject<JObject>(s)?.SelectToken("input") as JObject;
        }
    }
}
