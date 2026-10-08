using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace K4RMA.EditorTools
{
    public static class PrototypeTestRunner
    {
        public static void RunEditMode()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new Callback();
            api.RegisterCallbacks(callback);
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "K4RMA.Gameplay.Tests" }
            }));
        }

        sealed class Callback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("EditMode tests started. Cases=" + testsToRun.TestCaseCount);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite || result.FailCount == 0)
                    return;
                Debug.LogError(result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log("EditMode tests finished. Passed=" + result.PassCount + " Failed=" + result.FailCount + " Skipped=" + result.SkipCount);
                int code = result.FailCount > 0 || result.PassCount == 0 ? 1 : 0;
                EditorApplication.Exit(code);
            }
        }
    }
}
