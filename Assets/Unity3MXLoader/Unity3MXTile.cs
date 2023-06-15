using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity3MX
{
    //����Tile��Դ״̬
    public enum TileResourceState
    {
        UNLOADED,//��ʼδ���ػ���ж�ص�
        LOADING,//������
        PROCESSING,//������ɣ���������
        READY,//���ݴ������
        FAILED, //���ػ��������
    }

    public class Unity3MXTile
    {
        private Unity3MXComponent mRootComponent;
        private Unity3MXRootNode mRootNode;
        private string mId;
        public string id
        {
            get { return mId; }
        }
        private string mUrl;
        public string url
        {
            get { return mUrl; }
        }
        private Unity3MXTileNode mParentNode;
        public Unity3MXTileNode parentNode
        {
            get { return mParentNode; }
        }
        public bool isRootTile
        {
            get { return mParentNode == null; }
        }
        private Unity3MXBLoader mLoader;
        public Unity3MXBLoader loader
        {
            get { return mLoader; }
        }

        private TileResourceState mResourceState = TileResourceState.UNLOADED;
        public TileResourceState resourceState
        {
            get { return mResourceState; }
        }
        public bool isResourceUnloaded
        {
            get { return mResourceState == TileResourceState.UNLOADED; }
        }
        public bool isResourceLoading
        {
            get { return mResourceState == TileResourceState.LOADING; }
        }
        public bool isResourceProcessing
        {
            get { return mResourceState == TileResourceState.PROCESSING; }
        }
        public bool isResourceReady
        {
            get { return mResourceState == TileResourceState.READY; }
        }
        public bool isResourceFailed
        {
            get { return mResourceState == TileResourceState.FAILED; }
        }

        private int mRetryCount = 0;

        private GameObject mGameObject;
        internal GameObject gameObject
        {
            get
            {
                ensureGameObject();
                return mGameObject;
            }
        }

        private HeaderInfo mHeader;
        private Dictionary<string, ResourceInfo> mResourceCache = new();
        public Dictionary<string, ResourceInfo> resourceCache
        {
            get { return mResourceCache; }
        }
        private Dictionary<string, Unity3MXTileNode> mNodes = new();
        public bool isUninitialized
        {
            get { return mNodes.Count == 0; }
        }
        private List<Unity3MXTileNode> mReadyNodes = new();
        internal List<Unity3MXTileNode> readyNodes
        {
            get { return mReadyNodes; }
        }
        private bool mDestroyed = false;
        public bool isDestroyed
        {
            get { return mDestroyed; }
        }

        public Unity3MXTile(Unity3MXComponent rootComponent, Unity3MXRootNode rootNode, string id, Unity3MXTileNode parentNode = null)
        {
            mRootComponent = rootComponent;
            mRootNode = rootNode;
            mId = id;
            //��ȡurl
            if (parentNode == null)
                mUrl = rootComponent.baseDataUrl + id;
            else
                mUrl = rootNode.baseUrl + id;
            mParentNode = parentNode;
            mLoader = new Unity3MXBLoader(rootComponent, mUrl);
            mLoader.onError += onError;
            mLoader.onLoad += onLoad;
        }

        //����ʧ��
        private void onError(string error)
        {
            if (mDestroyed)
                return;
            Debug.LogError(error);
            mResourceState = TileResourceState.FAILED;
        }

        //���سɹ�
        private void onLoad(HeaderInfo header)
        {
            if (mDestroyed)
                return;
            mHeader = header;
            //״̬����LOADING��˵��δ������clearResourceCache
            if (mResourceState == TileResourceState.LOADING)
            {
                //����״̬ΪPROCESSING
                mResourceState = TileResourceState.PROCESSING;
            }
            //״̬����UNLOADED��˵��������clearResourceCache��ֱ������resources
            else if (mResourceState == TileResourceState.UNLOADED)
            {
                //������header�е�resources
                mHeader.resources.Clear();
                mHeader.resources = null;
            }
        }

        public void Update(CameraState cameraState)
        {
            if (mDestroyed)
                return;
            //δ��ʼ����˵����δ���ع���Դ��ִ����Դ����
            if (isUninitialized)
                StartResourceLoad();
            //������ɣ�ִ�����ݴ���
            if (isResourceProcessing)
            {
                //����Դ�������Resource����
                mResourceCache.Clear();
                foreach (var resource in mHeader.resources)
                {
                    mResourceCache.Add(resource.id, resource);
                }
                //������ɺ�������header�е�resources
                mHeader.resources.Clear();
                mHeader.resources = null;
                //δ����Node������ʼ���ɣ������ɹ��Ĳ����ظ�����
                foreach (var nodeInfo in mHeader.nodes)
                {
                    if (!mNodes.ContainsKey(nodeInfo.id))
                    {
                        var node = new Unity3MXTileNode(mRootComponent, mRootNode, this, nodeInfo);
                        mNodes.Add(nodeInfo.id, node);
                        node.Show();
                    }
                }
                //������ɺ�������header�е�nodes
                mHeader.nodes.Clear();
                mHeader.nodes = null;
                //����״̬ΪREADY
                mResourceState = TileResourceState.READY;
            }
            //����ʧ�ܣ�����
            else if (isResourceFailed)
            {
                StartResourceLoad();
            }
            //ִ��node.Update
            foreach (var node in mNodes.Values)
            {
                node.Update(cameraState);
            }
        }

        private void loadResource()
        {
            mResourceState = TileResourceState.LOADING;
            mRootComponent.StartCoroutine(mLoader.Load());
        }

        //��ʼ������Դ
        public void StartResourceLoad()
        {
            if (mDestroyed)
                return;
            //��ǰ��Դδ���أ���ִ�м���
            if (isResourceUnloaded)
            {
                loadResource();
            }
            //����ʧ���ˣ��ж����Դ����Ƿ�С��failRetryCount�������¼���
            else if (isResourceFailed && mRetryCount < mRootComponent.failRetryCount)
            {
                mRetryCount++;
                loadResource();
            }
        }

        //ȷ��gameObject��Ϊ��
        private void ensureGameObject()
        {
            if (mGameObject == null)
            {
                mGameObject = new GameObject(mId);
                //������RootNode��
                mGameObject.transform.SetParent(mRootNode.gameObject.transform, false);
            }
        }

        //����gameObject
        internal void destroyGameObject()
        {
            if (mGameObject != null)
            {
                Object.Destroy(mGameObject);
                mGameObject = null;
            }
        }

        //�����Դ����
        private void clearResourceCache()
        {
            mResourceCache.Clear();
            //����״̬ΪUNLOADED
            mResourceState = TileResourceState.UNLOADED;
        }

        //ж�ص�ǰTile
        public void Unload(bool recursivelyChildren = false)
        {
            //��������node.Unload
            foreach (var node in mNodes.Values)
            {
                node.Unload(recursivelyChildren);
            }
            destroyGameObject();
        }

        //���ٵ�ǰTile
        public void Destroy()
        {
            if (mDestroyed)
                return;

            foreach (var node in mNodes.Values)
            {
                node.Destroy();
            }
            mNodes.Clear();
            mReadyNodes.Clear();

            mLoader.onError -= onError;
            mLoader.onLoad -= onLoad;
            mLoader.Cancel();
            destroyGameObject();
            clearResourceCache();

            mDestroyed = true;
        }
    }

    public class Unity3MXTileNode
    {
        private Unity3MXComponent mRootComponent;
        private Unity3MXRootNode mRootNode;
        private Unity3MXTile mTile;
        public Unity3MXTile tile
        {
            get { return mTile; }
        }
        private NodeInfo mNodeInfo;
        private GameObject mGameObject;
        public bool isReady
        {
            get { return mGameObject != null; }
        }
        private List<Unity3MXTile> mChildTiles = new List<Unity3MXTile>();
        private bool mDestroyed = false;
        public bool isDestroyed
        {
            get { return mDestroyed; }
        }

        public Unity3MXTileNode(Unity3MXComponent rootComponent, Unity3MXRootNode rootNode, Unity3MXTile ownerTile, NodeInfo nodeInfo)
        {
            mRootComponent = rootComponent;
            mRootNode = rootNode;
            mTile = ownerTile;
            mNodeInfo = nodeInfo;
        }

        public void Update(CameraState cameraState)
        {
            if (mDestroyed)
                return;
            //��ȡ��ǰNode���ĵ���������
            var center = mRootNode.gameObject.transform.TransformPoint(mNodeInfo.bounds.center);
            //���㵱ǰNode����Ļ�ϵĵ�λ���ش�С������/����
            var pixelSize = cameraState.ComputePixelSize(center);
            //����boundingSphere����Ļ�ϵ�ͶӰ����ֱ��
            var diameter = mNodeInfo.boundingSphereRadius / pixelSize * mRootComponent.diameterRatio;
            //��ǰNode��boundingSphereͶӰ����ֱ������maxScreenDiameter�Ҵ�����һ��tile���������һ����
            if (diameter > mNodeInfo.maxScreenDiameter && mNodeInfo.children.Count > 0)
            {
                ensureChildTiles();
                //�ж���Tile�Ƿ�����ɳ�ʼ��
                bool isUninitialized = false;
                foreach (var tile in mChildTiles)
                {
                    tile.Update(cameraState);
                    isUninitialized = tile.isUninitialized || isUninitialized;
                }
                //����Tile��δ��ʼ��
                if (isUninitialized)
                {
                    //������ʾ��ǰNode
                    Show();
                }
                //ȫ����Tile������ɳ�ʼ��
                else
                {
                    //��ǵ�ǰNodeΪ׼��ж��
                    mRootComponent.AddReadyUnloadNode(this);
                }
            }
            else//����Ҫ������һ����ʹ�õ�ǰnode
            {
                //�ж���Դ�Ƿ��Ѽ������
                if (mTile.isResourceReady)
                {
                    //�Ѽ�������ʾ��ǰ�ڵ�
                    Show();
                    //������Tile��enableMemeoryCacheΪtrueʱֻж�ز�����
                    if (mRootComponent.enableMemeoryCache)
                    {
                        UnloadChildren();
                    }
                    //Ϊfalseʱ����������Tile
                    else
                    {
                        DestroyChildren();
                    }
                }
                //��Դδ����
                else
                {
                    //������Դ
                    mTile.StartResourceLoad();
                    //����Ƿ���Ҫ����ǰNode�ĸ�Node��ж�ر���б����Ƴ�
                    mRootComponent.CheckAndRemoveParentNode(this);
                }
            }
        }

        //�ж���Tile�Ƿ��Ѵ�����δ�����򴴽�
        private void ensureChildTiles()
        {
            if (mChildTiles.Count == 0)
            {
                for (int i = 0; i < mNodeInfo.children.Count; i++)
                {
                    mChildTiles.Add(new Unity3MXTile(mRootComponent, mRootNode, mNodeInfo.children[i], this));
                }
            }
        }

        //��ʾ��ǰNode
        public void Show()
        {
            if (mDestroyed || mGameObject != null)
                return;
            //����GameObject
            mGameObject = new GameObject(mNodeInfo.id);
            var collider = mGameObject.AddComponent<BoxCollider>();
            collider.center = mNodeInfo.bounds.center;
            collider.size = mNodeInfo.bounds.size;
            //���ص�Tile��
            mGameObject.transform.SetParent(mTile.gameObject.transform, false);

            foreach (var key in mNodeInfo.resources)
            {
                var resource = mTile.resourceCache[key];
                if (resource.type == "geometryBuffer")
                {
                    //����
                    if (resource.format == "ctm")
                    {
                        var meshObject = new GameObject(resource.id);
                        meshObject.transform.SetParent(mGameObject.transform, false);

                        var meshFilter = meshObject.AddComponent<MeshFilter>();
                        meshFilter.mesh = MeshData.ConstructMesh(resource.meshData);

                        var textureRes = mTile.resourceCache[resource.texture];
                        if (textureRes != null)
                        {
                            var meshRender = meshObject.AddComponent<MeshRenderer>();
                            meshRender.material = new Material(Shader.Find("HDRP/Lit"));
                            //����Texture
                            var texture = new Texture2D(1, 1);
                            texture.LoadImage(textureRes.textureData);
                            meshRender.material.mainTexture = texture;
                            //meshRender.material.SetTexture("_BaseColorMap", texture);
                            meshRender.material.SetFloat("_Smoothness", 0);
                            meshRender.material.EnableKeyword("_BaseColorMap");
                            meshRender.material.EnableKeyword("_Smoothness");
                            meshRender.shadowCastingMode = mRootComponent.shadowCastingMode;
                        }
                    }
                }
            }
            //���ӵ�readyNodes�б���
            if (!mTile.readyNodes.Contains(this))
                mTile.readyNodes.Add(this);
        }

        //ж��������Tile
        public void UnloadChildren()
        {
            foreach (var tile in mChildTiles)
            {
                tile.Unload(true);
            }
        }

        //ж�ص�ǰNode
        public void Unload(bool recursivelyChildren = false)
        {
            if (recursivelyChildren)
            {
                foreach (var tile in mChildTiles)
                {
                    tile.Unload(recursivelyChildren);
                }
            }
            if (mGameObject != null)
            {
                Object.Destroy(mGameObject);
                mGameObject = null;
                //��readyNodes�б����Ƴ�
                mTile.readyNodes.Remove(this);
                //readyNodes�б��ѿգ�˵��Tile������Node���ѱ�ж�أ�ִ��destroyGameObject
                if (mTile.readyNodes.Count == 0)
                    mTile.destroyGameObject();
            }
        }

        //����������Tile
        public void DestroyChildren()
        {
            foreach (var tile in mChildTiles)
            {
                tile.Destroy();
            }
            mChildTiles.Clear();
        }

        //���ٵ�ǰNode
        public void Destroy()
        {
            if (mDestroyed)
                return;

            DestroyChildren();

            if (mGameObject != null)
            {
                Object.Destroy(mGameObject);
                mGameObject = null;
            }

            mDestroyed = true;
        }
    }
}