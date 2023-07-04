using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

/**
 * Unity3MXLoader
 * 一个加载3MX模型的Unity插件
 * @author lijuhong1981
 */
namespace Unity3MX
{
    public class Unity3MXBLoader
    {
        private Unity3MXComponent mRootComponent;
        private string mUrl;
        public string url
        {
            get { return mUrl; }
        }

        private LoadTask mTask;
        public bool isLoading
        {
            get { return mTask != null; }
        }

        public delegate void OnError(string error);

        public event OnError onError;

        public delegate void OnLoad(HeaderInfo header);

        public event OnLoad onLoad;

        public Unity3MXBLoader(Unity3MXComponent rootComponent, string url)
        {
            mRootComponent = rootComponent;
            mUrl = url;
        }

        public void Cancel()
        {
            if (!isLoading)
            {
                Debug.LogWarning("This loader " + mUrl + " is not loading, not need to call Cancel method.");
                return;
            }
            mTask.Cancel();
            mTask = null;
        }

        public void Load()
        {
            if (isLoading)
            {
                Debug.LogWarning("This loader " + mUrl + " is loading, please wait load complete or call Cancel method before Load.");
                return;
            }
            mTask = new LoadTask(this);
            mRootComponent.taskPool.Add(mTask);
        }

        internal void notifyOnLoad(HeaderInfo header)
        {
            onLoad?.Invoke(header);
        }

        internal void notifyOnError(string error)
        {
            onError?.Invoke(error);
        }
    }

    class LoadTask : TaskBase
    {
        private Unity3MXBLoader mLoader;
        private long mStartTime;

        public LoadTask(Unity3MXBLoader loader)
        {
            mLoader = loader;
        }

        protected override void runInThread()
        {
            //Debug.Log("Start: " + mLoader.url);
            if (IsCanceled)
                return;
            try
            {
                //mStartTime = DateTime.Now.ToFileTime();
                string url = mLoader.url;
                bool isHttp = false;
                if (url.StartsWith("http"))
                {
                    isHttp = true;
                }
                else if (url.StartsWith("file:///"))
                {
                    url = url.Substring(8);
                }
                else if (url.StartsWith("file://"))
                {
                    url = url.Substring(7);
                }
                if (isHttp)
                {
                    HttpWebRequest request = WebRequest.CreateHttp(url);
                    using (var response = request.GetResponse())
                    {
                        //网络流，不能Seek或Position，所以需要先读取至一个MemoryStream中
                        using (var rs = response.GetResponseStream())
                        {
                            using (var ms = new MemoryStream())
                            {
                                //创建缓冲区
                                var buffer = new byte[1024 * 32];
                                while (true)
                                {
                                    int num = rs.Read(buffer, 0, buffer.Length);
                                    if (num == 0)
                                        break;
                                    ms.Write(buffer, 0, num);
                                    if (IsCanceled)
                                        return;
                                }
                                //重置Position
                                ms.Position = 0;
                                parse(ms);
                            }
                            //parse(rs);
                        }
                    }
                }
                else
                {
                    using (var stream = new FileStream(url, FileMode.Open, FileAccess.Read))
                    {
                        parse(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                string error = "Load " + mLoader.url + " failed, " + ex;
                Debug.LogWarning(error);
                mLoader.notifyOnError(error);
            }
        }

        private void parse(Stream stream)
        {
            using (BinaryReader br = new BinaryReader(stream))
            {
                if (IsCanceled)
                    return;
                // magic number
                string magicNumber = new(br.ReadChars(5));
                if (magicNumber != "3MXBO")
                {
                    string error = "Unsupported magic number " + magicNumber + " in 3mxb file: " + mLoader.url;
                    Debug.LogWarning(error);
                    mLoader.notifyOnError(error);
                    return;
                }
                if (IsCanceled)
                    return;
                // header size
                uint headerSize = br.ReadUInt32();
                if (headerSize == 0)
                {
                    string error = "Unexpected zero length header in 3mxb file: " + mLoader.url;
                    Debug.LogWarning(error);
                    mLoader.notifyOnError(error);
                    return;
                }
                if (IsCanceled)
                    return;
                // header json
                string headerJson = new string(br.ReadChars((int)headerSize));
                //Debug.Log(headerJson);
                HeaderInfo header = HeaderInfo.FromJson(headerJson);
                // resources
                for (int i = 0; i < header.resources.Count; i++)
                {
                    if (IsCanceled)
                        return;
                    ResourceInfo resource = header.resources[i];
                    if (resource.size > 0)
                    {
                        if (resource.type == "textureBuffer")
                        {
                            resource.textureData = br.ReadBytes(resource.size);
                        }
                        else if (resource.type == "geometryBuffer")
                        {
                            if (resource.format == "ctm")
                            {
                                parseMesh(br, resource);
                            }
                            else
                            {
                                string error = "Unexpected buffer format " + resource.format + " in 3mxb file: " + mLoader.url;
                                Debug.LogWarning(error);
                                mLoader.notifyOnError(error);
                                continue;
                            }
                        }
                        else
                        {
                            string error = "Unexpected buffer type " + resource.type + " in 3mxb file: " + mLoader.url;
                            Debug.LogWarning(error);
                            mLoader.notifyOnError(error);
                            continue;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("This resource.size is 0, from file: " + mLoader.url + "; id: " + resource.id);
                    }
                }

                for (int i = 0; i < header.nodes.Count; i++)
                {
                    if (IsCanceled)
                        return;
                    NodeInfo node = header.nodes[i];
                    //根据bbMin与bbMax计算BoundingSphere
                    Vector3 bbMin = new Vector3(node.bbMin[0], node.bbMin[2], node.bbMin[1]);
                    Vector3 bbMax = new Vector3(node.bbMax[0], node.bbMax[2], node.bbMax[1]);
                    node.bounds = makeBounds(bbMin, bbMax);
                    node.bsRadius = node.bounds.size.magnitude / 2;
                }

                header.url = mLoader.url;

                if (IsCanceled)
                    return;

                //var usingTime = (DateTime.Now.ToFileTime() - mStartTime) / 10000L;
                //Debug.Log("Finished: " + mLoader.url + "; usingTime: " + usingTime);

                mLoader.notifyOnLoad(header);
            }
        }

        private Bounds makeBounds(Vector3 bbMin, Vector3 bbMax)
        {
            Vector3 center = (bbMin + bbMax) / 2;
            Vector3 size = bbMax - bbMin;
            return new Bounds(center, size);
        }

        private MeshData parseMesh(BinaryReader br, ResourceInfo resource)
        {
            Vector3 bbMin = new Vector3(resource.bbMin[0], resource.bbMin[2], resource.bbMin[1]);
            Vector3 bbMax = new Vector3(resource.bbMax[0], resource.bbMax[2], resource.bbMax[1]);

            OpenCTM.CtmFileReader reader = new OpenCTM.CtmFileReader(br.BaseStream);
            OpenCTM.Mesh mesh = reader.decode();

            int vertexCount = mesh.getVertexCount();
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            bool hasNormals = mesh.hasNormals();
            Vector3[] normals = null;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].x = mesh.vertices[(i * 3)];
                vertices[i].y = mesh.vertices[(i * 3) + 2];
                vertices[i].z = mesh.vertices[(i * 3) + 1];
                if (mesh.texcoordinates.Length > 0)
                {
                    uv[i].x = mesh.texcoordinates[0].values[(i * 2)];
                    uv[i].y = mesh.texcoordinates[0].values[(i * 2) + 1];
                }
                if (hasNormals)
                {
                    if (normals == null)
                        normals = new Vector3[vertexCount];
                    normals[i].x = mesh.normals[(i * 3)];
                    normals[i].y = mesh.normals[(i * 3) + 2];
                    normals[i].z = mesh.normals[(i * 3) + 1];
                }
            }
            int[] triangles = new int[mesh.indices.Length];
            int length = mesh.indices.Length / 3;
            for (int j = 0; j < length; j++)
            {
                triangles[(j * 3)] = mesh.indices[(j * 3)];
                triangles[(j * 3) + 1] = mesh.indices[(j * 3) + 2];
                triangles[(j * 3) + 2] = mesh.indices[(j * 3) + 1];
            }

            MeshData result = new MeshData();
            result.vertexCount = vertexCount;
            result.vertices = vertices;
            result.uv = uv;
            result.hasNormals = hasNormals;
            result.normals = normals;
            result.triangles = triangles;
            result.bbMin = bbMin;
            result.bbMax = bbMax;
            result.bounds = makeBounds(bbMin, bbMax);

            resource.meshData = result;

            return result;
        }
    }
}
