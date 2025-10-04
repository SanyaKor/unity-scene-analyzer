using YamlDotNet.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnitySceneAnalyzer
{
    public class Scene
    {
        public readonly Dictionary<long, UnityObject> sceneObjects;
        public readonly string scenePath;
        private string sceneText;
        private IDeserializer yamlDeserializer;
        public Scene(string path)
        {
            ///TODO check file
            scenePath = path;
            sceneObjects = new Dictionary<long, UnityObject>();
            yamlDeserializer = new DeserializerBuilder().Build();
        }
        
        public void ReadSceneFromFile()
        {
            sceneText = File.ReadAllText(scenePath);
        }
        
        public async Task ReadSceneFromFileAsync()
        {
            sceneText = await File.ReadAllTextAsync(scenePath);
        }
        public void ParseSceneFromText()
        {   
            var yamlBlocks = sceneText
                .Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1);

            foreach (var yamlBlock in yamlBlocks)
            {
                var lines = yamlBlock.Split('\n');
                if (lines.Length < 3) continue;

                var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string objectType = parts[0];
                if (!long.TryParse(parts[1].TrimStart('&'), out var objectId)) continue;

                string objectStructure = string.Join("\n", lines[2..]);
                var properties = yamlDeserializer.Deserialize<Dictionary<string, object>>(objectStructure);

                var obj = new UnityObject(objectId, Scene.Parse(objectType), properties);
                sceneObjects[objectId] = obj;
            }
        }
        
        public string GetSceneGameObejctsHierarchy()
        {
            var sb = new StringBuilder();
            
            //TODO null values
            List<UnityObject> transforms = GetAllObjectsByType(UnityClassId.Transform);
            List<UnityObject> gameObjects = GetAllObjectsByType(UnityClassId.GameObject);

            var parentOf = new Dictionary<long, long>();  
            var childrenOf = new Dictionary<long, List<long>>();
            
            var transformById= transforms.ToDictionary(t => t.objectId, t => t);
            var gameObjectById= gameObjects.ToDictionary(g => g.objectId, g => g);

            
            foreach (var t in transforms)
            {
                if (!childrenOf.ContainsKey(t.objectId))
                    childrenOf[t.objectId] = new List<long>();

                var children = t.GetProperty<List<object>>("m_Children");
                if (children == null) continue;

                foreach (var child in children.OfType<Dictionary<object, object>>())
                {
                    if (long.TryParse(child["fileID"].ToString(), out var childId))
                    {
                        childrenOf[t.objectId].Add(childId);
                        parentOf[childId] = t.objectId;
                    }
                }
            }

            var rootIds = transforms.Where(t => !parentOf.ContainsKey(t.objectId))
                .Select(t => t.objectId)
                .ToList();

            
            var stack = new Stack<(long id, int depth)>();
            
            for (int i = rootIds.Count - 1; i >= 0; i--)
            {
                stack.Push((rootIds[i], 0));
            }

            while (stack.Count > 0)
            {
                var (tid, depth) = stack.Pop();

                if (!transformById.TryGetValue(tid, out var t))
                {
                    continue;
                }

                string name = "(unnamed)";
                var goRef = t.GetProperty<Dictionary<object, object>>("m_GameObject");

                if (goRef != null &&
                    goRef.TryGetValue("fileID", out var goIdObj) &&
                    long.TryParse(goIdObj?.ToString(), out var goId) &&
                    gameObjectById.TryGetValue(goId, out var go))
                {
                    name = go.GetProperty<string>("m_Name") ?? "(unnamed)";
                }

                sb.AppendLine($"{new string('-', depth * 2)}{name}");

                if (childrenOf.TryGetValue(tid, out var kids) && kids.Count > 0)
                {
                    for (int i = kids.Count - 1; i >= 0; i--)
                    {
                        stack.Push((kids[i], depth + 1));
                    }
                }
            }


            return sb.ToString();
        }
        
        public UnityObject? GetObjectById(long objectId)
        {
            foreach (var gameObj in sceneObjects)
            {
                if (gameObj.Key == objectId)
                {
                    return gameObj.Value;
                }
            }
            return null;
        }
        public List<UnityObject>? GetAllObjectsByType(UnityClassId classId)
        {
            List<UnityObject> objectsList = new List<UnityObject>();
            
            foreach (var gameObj in sceneObjects)
            {
                if (gameObj.Value.typeId == classId)
                {
                    objectsList.Add(gameObj.Value);
                }
            }
            return objectsList.Count > 0 ? objectsList : null;
        }
        private static UnityClassId Parse(string tag)
        {
            if (tag.StartsWith("!u!"))
            {
                if (int.TryParse(tag.Substring(3), out int id))
                {
                    if (Enum.IsDefined(typeof(UnityClassId), id))
                        return (UnityClassId)id;
                
                    return UnityClassId.Unknown;
                }
            }

            return UnityClassId.Unknown;
        }

    }
    
    public class UnityObject 
    {
        public readonly long objectId;
        public readonly UnityClassId typeId;
        public readonly Dictionary<string, object> properties;

        public UnityObject(long objectId, UnityClassId typeId,
            Dictionary<string, object> properties)
        {
            this.objectId = objectId;
            this.typeId = typeId;
            this.properties = properties;
        }
        public T? GetProperty<T>(string key)
        {
            if (properties.TryGetValue(key, out var value) && value is T t)
                return t;
            return default;
        }

    }
    
    public enum UnityClassId
    {
        GameObject = 1,
        Transform = 4,
        Camera = 20,
        MeshRenderer = 23,
        Unknown = -1
    };
}
