using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * Unity3MXLoader
 * 一个加载3MX模型的Unity插件
 * @author lijuhong1981
 */
namespace Unity3MX
{
    public class NodeInfo
    {
        public string id;

        /// <summary>An array of 3 numbers that define an min of the bounding box.</summary>
        public List<float> bbMin = new();

        /// <summary>An array of 3 numbers that define an max of the bounding box.</summary>
        public List<float> bbMax = new();

        public float maxScreenDiameter;

        public List<string> children = new();

        public List<string> resources = new();

        [Newtonsoft.Json.JsonIgnore]
        public Bounds bounds;

        [Newtonsoft.Json.JsonIgnore]
        public float boundingSphereRadius;

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static NodeInfo FromJson(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<NodeInfo>(json);
        }
    }

    public class ResourceInfo
    {
        public string id;

        public string type;

        public string format;

        public int size;

        public string file;

        /// <summary>An array of 3 numbers that define an min of the bounding box.</summary>
        public List<float> bbMin = new();

        /// <summary>An array of 3 numbers that define an max of the bounding box.</summary>
        public List<float> bbMax = new();

        public string texture;

        [Newtonsoft.Json.JsonIgnore]
        public byte[] textureData;

        [Newtonsoft.Json.JsonIgnore]
        public MeshData meshData;

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ResourceInfo FromJson(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ResourceInfo>(json);
        }
    }

    public class HeaderInfo
    {
        [Newtonsoft.Json.JsonIgnore]
        public string url;

        public int version;

        public List<NodeInfo> nodes = new();

        public List<ResourceInfo> resources = new();

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static HeaderInfo FromJson(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<HeaderInfo>(json);
        }
    }

    //public struct TextureData
    //{
    //    public int width;
    //    public int height;
    //    public TextureFormat format;
    //    public bool streamingMipmaps;
    //}

    public struct MeshData
    {
        public int vertexCount;
        public Vector3[] vertices;
        public Vector2[] uv;
        public bool hasNormals;
        public Vector3[] normals;
        public int[] triangles;
        public Vector3 bbMin;
        public Vector3 bbMax;
        public Bounds bounds;

        public static UnityEngine.Mesh ConstructMesh(MeshData data)
        {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            mesh.vertices = data.vertices;
            mesh.uv = data.uv;
            mesh.triangles = data.triangles;
            if (data.hasNormals)
                mesh.normals = data.normals;
            else
                mesh.RecalculateNormals();
            mesh.bounds.SetMinMax(data.bbMin, data.bbMax);
            return mesh;
        }
    }
}
