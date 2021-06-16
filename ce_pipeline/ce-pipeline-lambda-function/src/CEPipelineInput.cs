namespace ce_pipeline_lambda_function
{
    public class CEPipelineInput
    {
        public string StepName { get; set; }
        public string TaskToken { get; set; }
        public string BucketName { get; set; }
        public string UploadFile { get; set; }
        public string InstanceId { get; set; }
    }
}