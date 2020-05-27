using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KinesisFirehose;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Lambda;
using s3 = Amazon.CDK.AWS.S3;
using System;

namespace CentralLogsCdk
{
    public class LogDestinationStack : Stack
    {
        internal LogDestinationStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            this.DestinationAccountId = props.Env.Account;
            
            var sourceAcctNumParam = new CfnParameter(this, "SourceAccountNumber", new CfnParameterProps
            {
                Description = "Account Number which is given access to push the logs."
            });

            if (!string.IsNullOrEmpty(sourceAcctNumParam.ValueAsString))
            {Console.WriteLine("SourceAccountNumber: ----------- " + sourceAcctNumParam.ValueAsString);}
            this.SourceLogAccountId = sourceAcctNumParam.ValueAsString ?? props.Env.Account;

            CreateLogBucket();
            CreateLogConsumerResources();           
        }

        public string SourceLogAccountId { get; set; }
        public string DestinationAccountId { get; set; }
        public string LogDestinationArn { get; set; }
        private s3.Bucket _logsBucket;

        public void CreateLogBucket()
        {
            _logsBucket = new s3.Bucket(this, "centrallogsbucket", new s3.BucketProps{
                BucketName = "central-logs-" + this.Account
            });
        }

        public void CreateLogConsumerResources()
        {
            //LambdaRole
            var firehoseLambdaRole = new Role(this, "FirehoseLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                Path = "/",
            });

            firehoseLambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new string[] { "arn:aws:logs:*:*:*" },
                Actions = new string[] { "logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents" },
            }));

            //FirehoseDataProcessingFunction
            var handler = new Function(this, "FirehoseDataProcessorFunction", new FunctionProps
            {
                FunctionName = "data-processor-function",
                Runtime = Runtime.NODEJS_12_X,
                Code = Code.FromAsset("resources"),
                Handler = "index.handler",
                Role = firehoseLambdaRole,
                Timeout = Duration.Minutes(2)
            });            

            //FirehoseDeliveryRole & Policies
            var firehoseDeliveryRole = new Role(this, "FirehoseDeliveryRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("firehose.amazonaws.com"),
                Path = "/"
            });

            //S3 permissions
            firehoseDeliveryRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new string[] { _logsBucket.BucketArn, _logsBucket.BucketArn + "/*" },
                Actions = new string[] { "s3:AbortMultipartUpload", "s3:GetBucketLocation", "s3:GetObject"
                , "s3:ListBucket", "s3:ListBucketMultipartUploads", "s3:PutObject" },
            }));

            //Lambda permissions
            firehoseDeliveryRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new string[] { handler.FunctionArn },
                Actions = new string[] {"lambda:GetFunctionConfiguration", "lambda:InvokeFunction" },
            }));

            //Log group for Firehose logs.
            var firehoseloggroup = new LogGroup(this, "firehoseloggroup", new LogGroupProps
            {
                LogGroupName = "central-logs-delivery-group"
            });
            var logstream = new LogStream(this, "logstream", new LogStreamProps
            {
                LogStreamName = "central-logs-delivery-stream",
                LogGroup = firehoseloggroup
            });

            firehoseDeliveryRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new string[] { firehoseloggroup.LogGroupArn },
                Actions = new string[] { "logs:PutLogEvents"},
            }));

            //FirehoseLoggingDeliveryStream - Start
            CfnDeliveryStream.ExtendedS3DestinationConfigurationProperty s3config = new CfnDeliveryStream.ExtendedS3DestinationConfigurationProperty();
            s3config.BucketArn = _logsBucket.BucketArn;
            s3config.BufferingHints = new CfnDeliveryStream.BufferingHintsProperty
            {
                SizeInMBs = 50,
                IntervalInSeconds = 300
            };
            s3config.CompressionFormat = "UNCOMPRESSED";
            s3config.RoleArn = firehoseDeliveryRole.RoleArn;
            s3config.Prefix = "CentralLogs/AWSLogs/";
            s3config.ErrorOutputPrefix = "CentralLogs/AWSLogs/Error/";

            var parameters = new CfnDeliveryStream.ProcessorParameterProperty();
            parameters.ParameterName = "LambdaArn";
            parameters.ParameterValue = handler.FunctionArn;

            var paramsArray1 = new CfnDeliveryStream.ProcessorParameterProperty[] { parameters };

            var processorProperty = new CfnDeliveryStream.ProcessorProperty();
            processorProperty.Parameters = paramsArray1;
            processorProperty.Type = "Lambda";

            var paramsArray = new CfnDeliveryStream.ProcessorProperty[] { processorProperty };

            s3config.ProcessingConfiguration = new CfnDeliveryStream.ProcessingConfigurationProperty
            {
                Enabled = true,
                Processors = paramsArray
            };            

            s3config.CloudWatchLoggingOptions = new CfnDeliveryStream.CloudWatchLoggingOptionsProperty
            {
                Enabled = true,
                LogGroupName = firehoseloggroup.LogGroupName,
                LogStreamName = logstream.LogStreamName
            };


            CfnDeliveryStream firehoseDeliveryStream = new CfnDeliveryStream(this, "FirehoseLoggingDeliveryStream", new CfnDeliveryStreamProps
            {
                DeliveryStreamType = "DirectPut",
                ExtendedS3DestinationConfiguration = s3config
            });
            //FirehoseLoggingDeliveryStream - End

            //Policy Statements for LogDestination- start
            var policyStmt = new PolicyStatement(new PolicyStatementProps()
            {
                Actions = new string[] { "firehose:PutRecord" },
                Resources = new string[] { "*" },
                Effect = Effect.ALLOW
            });
            var policyDoc = new PolicyDocument();
            policyDoc.AddStatements(new PolicyStatement[] { policyStmt });
            
            var policyProp = new CfnRole.PolicyProperty();
            policyProp.PolicyName = "logDestinationPolicy";
            policyProp.PolicyDocument = policyDoc;
            //Policy Statements - end

            //AssumeRolePolicyDocument for LogDestination - start
            var principal = new ServicePrincipal("logs.amazonaws.com");
            var assumePolicyStatement = new PolicyStatement(new PolicyStatementProps
            {
                Actions = new string[] { "sts:AssumeRole" },
                Effect = Effect.ALLOW,
                Principals = new IPrincipal[] { principal }
            });
            var assumePolicyDoc = new PolicyDocument();
            assumePolicyDoc.AddStatements(new PolicyStatement[] { assumePolicyStatement });            
            //AssumeRolePolicyDocument - end

            var roleProps = new CfnRoleProps{
                Path = "/",
                AssumeRolePolicyDocument = assumePolicyDoc,
                Policies = new CfnRole.PolicyProperty[] { policyProp }
            };

            CfnRole cfnRole = new CfnRole(this, "CfnRole", roleProps);
            
            CfnDestination logDestination = new CfnDestination(this, "LogDestination", new CfnDestinationProps
            {
                DestinationName = "Central-Log-Destination",
                RoleArn = cfnRole.AttrArn,
                TargetArn = firehoseDeliveryStream.AttrArn,
                DestinationPolicy = "{\"Version\" : \"2012-10-17\",\"Statement\" : [{\"Effect\" : \"Allow\", \"Principal\" : {\"AWS\" :  [\"" + SourceLogAccountId + "\"]},\"Action\" : \"logs:PutSubscriptionFilter\", \"Resource\" : \"arn:aws:logs:" + this.Region + ":"
                + DestinationAccountId + ":destination:Central-Log-Destination\"}]}"
            });

            logDestination.AddDependsOn(firehoseDeliveryStream);
            logDestination.AddDependsOn(cfnRole);
            Console.WriteLine(logDestination.DestinationPolicy);

            LogDestinationArn = logDestination.AttrArn;
           
             CfnOutput output = new CfnOutput(this, "LogDestinationARN", new CfnOutputProps{
                Description = "LogDestination ARN",
                Value = logDestination.AttrArn
            });
        }
    }
}
