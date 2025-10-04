using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace UnitySceneAnalyzer
{
    public class SceneCollectionAnalyzer
    {
        public readonly Dictionary<int, Scene> Scenes = new Dictionary<int, Scene>();
        private string projectPath;
        private string outputPath;

        public SceneCollectionAnalyzer(string projectpath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(projectpath) || !Directory.Exists(projectpath))
                throw new DirectoryNotFoundException(projectpath);
            
            if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
                throw new DirectoryNotFoundException(outputPath);
            
            this.projectPath = projectpath;
            this.outputPath = outputPath;
            int sceneId = 0;
            
            List<string> sceneNames = Directory.EnumerateFiles(projectpath, "*.unity", SearchOption.AllDirectories).ToList();

            foreach (string sceneName in sceneNames)
            {
                Scene scene = new Scene(sceneName);
                Scenes[sceneId] = scene;
                sceneId++;
            }
        }
        
        public void runAnalyzer()
        {
            var sw = Stopwatch.StartNew();

            foreach (var scene in Scenes.Values.OrderBy(s => Path.GetFileNameWithoutExtension(s.scenePath)))
            {
                scene.ReadSceneFromFile();
                scene.ParseSceneFromText();
                string dump = scene.GetSceneGameObejctsHierarchy();

                string fileName = Path.GetFileNameWithoutExtension(scene.scenePath) + ".unity.dump";
                string outPath  = Path.Combine(outputPath, fileName);
                File.WriteAllText(outPath, dump, new UTF8Encoding(false)); // I/O sync

                Console.WriteLine($"[SYNC] dumped: {fileName}");
            }

            sw.Stop();
            Console.WriteLine($"[SYNC] total: {sw.ElapsedMilliseconds} ms");
        }

        public async Task runAnalyzerAsync()
        {
            var sw = Stopwatch.StartNew();

            var pending = Scenes.Values
                .Select(async s =>
                {
                    await s.ReadSceneFromFileAsync();
                    return s;
                })
                .ToList();

            var postTasks = new List<Task>();

            while (pending.Count > 0)
            {
                var finished = await Task.WhenAny(pending);
                pending.Remove(finished);

                var scene = await finished;

                postTasks.Add(Task.Run(async () =>
                {
                    scene.ParseSceneFromText();

                    string dump = scene.GetSceneGameObejctsHierarchy();

                    string fileName = Path.GetFileNameWithoutExtension(scene.scenePath) + ".unity.dump";
                    string outPath  = Path.Combine(outputPath, fileName);
                    await File.WriteAllTextAsync(outPath, dump, new UTF8Encoding(false));

                    Console.WriteLine($"[DUMPED] {fileName}");
                }));
            }

            await Task.WhenAll(postTasks);

            sw.Stop();
            Console.WriteLine($"[ASYNC] total: {sw.ElapsedMilliseconds} ms");
        }
    }
}