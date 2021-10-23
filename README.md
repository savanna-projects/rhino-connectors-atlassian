# Rhino Connectors: Atlassian
Rhino API connectors for using with Atlassian products and plugins.  

## Automation Provider Capabilites
The list of optional capabilities which you can pass with Rhino ProviderConfiguration.  

The options must be passed under `<connector_name>:options` key, as follow:

```js
...
"capabilities": {
	"connector_xray:options": {
		"testType": "XRay Test",
		"setType": "XRay Set",
		...
	}
	"connector_xray_cloud:options": {
		"testType": "Cloud Test",
		"setType": "Cloud Set"
		...
	}
}
...
```

|Name              |Type   |Description                                                                                                   |
|------------------|-------|--------------------------------------------------------------------------------------------------------------|
|testType          |string |Test case issue type capability, if not set "Test" is the default.                                            |
|setType           |string |Test set issue type capability, if not set "Test Set" is the default.                                         |
|preconditionsType |string |Test preconditions issue type capability, if not set "Pre-Condition" is the default.                          |
|planType          |string |Test plan issue type capability, if not set "Test Plan" is the default.                                       |
|executionType     |string |Test execution issue type capability, if not set "Test Plan" is the default.                                  |
|bugType           |string |Bug issue type capability, if not set "Bug" is the default.                                                   |
|bucketSize        |number |How many parallel requests can be sent to Jira/XRay API when executing a large number of tests. Default is 4. |
|testPlans         |array  |Holds test plans keys. If set, when test is created it will also be associated with these test plans.         |
|jiraApiVersion    |string |The Jira API version to use when executing requests against Jira API. If not specified, "latest" will be used.|
|syncFields        |array  |A list of custom fields to sync between `Test` and `Bug` when bug is created the value will be taken from the test entitiy.|
|customFields      |object |Static key/value for custom fields when creating `Test`, `Bug` or `Test Execution`. The key/value are the filed name and value as appears in the issue screen.                                             |
|inconclusiveStatus|string |The status which will be assigned to a test case when the test result is inconclusive. Inconclusive can happen when test have no assertions or violating pass/fail thresholds such as priority or severity.|