using System.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnitySceneAnalyzer
{
    class Program
    {

        static async Task Main(string[] args)
        {

            string path = "/Users/allebedev/RiderProjects/UnitySceneAnalyzer/UnitySceneAnalyzer/Testing/Samples/";
            string output = "/Users/allebedev/RiderProjects/UnitySceneAnalyzer/UnitySceneAnalyzer/Testing/Results/";

            SceneCollectionAnalyzer sca = new SceneCollectionAnalyzer(path, output);

            //await sca.runAnalyzerAsync();
            sca.runAnalyzer();
        }

    }
        
}