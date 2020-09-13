# Rhino Connectors: Atlassian
Rhino API connectors for using with Atlassian products and plugins.  

## Automation Provider Capabilites
The list of optional capabilities which you can pass with Rhino ProviderConfiguration.  

|Name             |Type   |Description                                                                                                  |
|-----------------|-------|-------------------------------------------------------------------------------------------------------------|
|testType         |string |Test case issue type capability, if not set "Test" is the default.                                           |
|setType          |string |Test set issue type capability, if not set "Test Set" is the default.                                        |
|preconditionsType|string |Test preconditions issue type capability, if not set "Pre-Condition" is the default.                         |
|planType         |string |Test plan issue type capability, if not set "Test Plan" is the default.                                      |
|executionType    |string |Test execution issue type capability, if not set "Test Plan" is the default.                                 |
|bugType          |string |Bug issue type capability, if not set "Bug" is the default.                                                  |
|dryRun           |boolean|Holds a boolean value rather or not to create Test Execution entity when running tests.                      |
|bucketSize       |number |How many parallel requests can be sent to Jira/XRay API when executing a large number of tests. Default is 4.|
|testPlans        |array  |Holds test plans keys. If set, when test is created it will also be associated with these test plans.        |