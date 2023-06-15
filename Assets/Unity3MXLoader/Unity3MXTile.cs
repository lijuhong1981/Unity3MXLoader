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
    //定义Tile资源状态
    public enum TileResourceState
    {
        UNLOADED,//初始未加载或已卸载的
        LOADING,//加载中
        PROCESSING,//加载完成，处理数据
        READY,//数据处理完成
        FAILED, //加载或解析出错
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
            //获取url
            if (parentNode == null)
                mUrl = rootComponent.baseDataUrl + id;
            else
                mUrl = rootNode.baseUrl + id;
            mParentNode = parentNode;
            mLoader = new Unity3MXBLoader(rootComponent, mUrl);
            mLoader.onError += onError;
            mLoader.onLoad += onLoad;
        }

        //加载失败
        private void onError(string error)
        {
            if (mDestroyed)
                return;
            Debug.LogError(error);
            mResourceState = TileResourceState.FAILED;
        }

        //加载成功
        private void onLoad(HeaderInfo header)
        {
            if (mDestroyed)
                return;
            mHeader = header;
            //状态处于LOADING，说明未被调用clearResourceCache
            if (mResourceState == TileResourceState.LOADING)
            {
                //设置状态为PROCESSING
                mResourceState = TileResourceState.PROCESSING;
            }
            //状态处于UNLOADED，说明调用了clearResourceCache，直接清理resources
            else if (mResourceState == TileResourceState.UNLOADED)
            {
                //清理掉header中的resources
                mHeader.resources.Clear();
                mHeader.resources = null;
            }
        }

        public void Update(CameraState cameraState)
        {
            if (mDestroyed)
                return;
            //未初始化，说明还未加载过资源，执行资源加载
            if (isUninitialized)
                StartResourceLoad();
            //加载完成，执行数据处理
            if (isResourceProcessing)
            {
                //将资源对象放入Resource缓存
                mResourceCache.Clear();
                foreach (var resource in mHeader.resources)
                {
                    mResourceCache.Add(resource.id, resource);
                }
                //缓存完成后清理掉header中的resources
                mHeader.resources.Clear();
                mHeader.resources = null;
                //未生成Node对象则开始生成，已生成过的不用重复生成
                foreach (var nodeInfo in mHeader.nodes)
                {
                    if (!mNodes.ContainsKey(nodeInfo.id))
                    {
                        var node = new Unity3MXTileNode(mRootComponent, mRootNode, this, nodeInfo);
                        mNodes.Add(nodeInfo.id, node);
                        node.Show();
                    }
                }
                //生成完成后清理掉header中的nodes
                mHeader.nodes.Clear();
                mHeader.nodes = null;
                //设置状态为READY
                mResourceState = TileResourceState.READY;
            }
            //加载失败，重试
            else if (isResourceFailed)
            {
                StartResourceLoad();
            }
            //执行node.Update
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

        //开始加载资源
        public void StartResourceLoad()
        {
            if (mDestroyed)
                return;
            //当前资源未加载，则执行加载
            if (isResourceUnloaded)
            {
                loadResource();
            }
            //加载失败了，判断重试次数是否小于failRetryCount，是重新加载
            else if (isResourceFailed && mRetryCount < mRootComponent.failRetryCount)
            {
                mRetryCount++;
                loadResource();
            }
        }

        //确保gameObject不为空
        private void ensureGameObject()
        {
            if (mGameObject == null)
            {
                mGameObject = new GameObject(mId);
                //挂载至RootNode下
                mGameObject.transform.SetParent(mRootNode.gameObject.transform, false);
            }
        }

        //销毁gameObject
        internal void destroyGameObject()
        {
            if (mGameObject != null)
            {
                Object.Destroy(mGameObject);
                mGameObject = null;
            }
        }

        //清除资源缓存
        private void clearResourceCache()
        {
            mResourceCache.Clear();
            //设置状态为UNLOADED
            mResourceState = TileResourceState.UNLOADED;
        }

        //卸载当前Tile
        public void Unload(bool recursivelyChildren = false)
        {
            //调用所有node.Unload
            foreach (var node in mNodes.Values)
            {
                node.Unload(recursivelyChildren);
            }
            destroyGameObject();
        }

        //销毁当前Tile
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
            //获取当前Node中心点世界坐标
            var center = mRootNode.gameObject.transform.TransformPoint(mNodeInfo.bounds.center);
            //计算当前Node在屏幕上的单位像素大小，既米/像素
            var pixelSize = cameraState.ComputePixelSize(center);
            //计算boundingSphere在屏幕上的投影像素直径
            var diameter = mNodeInfo.boundingSphereRadius / pixelSize * mRootComponent.diameterRatio;
            //当前Node的boundingSphere投影像素直径大于maxScreenDiameter且存在下一级tile，则加载下一级别
            if (diameter > mNodeInfo.maxScreenDiameter && mNodeInfo.children.Count > 0)
            {
                ensureChildTiles();
                //判断子Tile是否都已完成初始化
                bool isUninitialized = false;
                foreach (var tile in mChildTiles)
                {
                    tile.Update(cameraState);
                    isUninitialized = tile.isUninitialized || isUninitialized;
                }
                //有子Tile还未初始化
                if (isUninitialized)
                {
                    //继续显示当前Node
                    Show();
                }
                //全部子Tile均已完成初始化
                else
                {
                    //标记当前Node为准备卸载
                    mRootComponent.AddReadyUnloadNode(this);
                }
            }
            else//不需要加载下一级，使用当前node
            {
                //判断资源是否已加载完毕
                if (mTile.isResourceReady)
                {
                    //已加载则显示当前节点
                    Show();
                    //处理子Tile，enableMemeoryCache为true时只卸载不销毁
                    if (mRootComponent.enableMemeoryCache)
                    {
                        UnloadChildren();
                    }
                    //为false时销毁所有子Tile
                    else
                    {
                        DestroyChildren();
                    }
                }
                //资源未加载
                else
                {
                    //加载资源
                    mTile.StartResourceLoad();
                    //检查是否需要将当前Node的父Node从卸载标记列表中移除
                    mRootComponent.CheckAndRemoveParentNode(this);
                }
            }
        }

        //判断子Tile是否已创建，未创建则创建
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

        //显示当前Node
        public void Show()
        {
            if (mDestroyed || mGameObject != null)
                return;
            //生成GameObject
            mGameObject = new GameObject(mNodeInfo.id);
            var collider = mGameObject.AddComponent<BoxCollider>();
            collider.center = mNodeInfo.bounds.center;
            collider.size = mNodeInfo.bounds.size;
            //挂载到Tile下
            mGameObject.transform.SetParent(mTile.gameObject.transform, false);

            foreach (var key in mNodeInfo.resources)
            {
                var resource = mTile.resourceCache[key];
                if (resource.type == "geometryBuffer")
                {
                    //网格
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
                            //生成Texture
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
            //添加到readyNodes列表中
            if (!mTile.readyNodes.Contains(this))
                mTile.readyNodes.Add(this);
        }

        //卸载所有子Tile
        public void UnloadChildren()
        {
            foreach (var tile in mChildTiles)
            {
                tile.Unload(true);
            }
        }

        //卸载当前Node
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
                //从readyNodes列表中移除
                mTile.readyNodes.Remove(this);
                //readyNodes列表已空，说明Tile下所有Node都已被卸载，执行destroyGameObject
                if (mTile.readyNodes.Count == 0)
                    mTile.destroyGameObject();
            }
        }

        //销毁所有子Tile
        public void DestroyChildren()
        {
            foreach (var tile in mChildTiles)
            {
                tile.Destroy();
            }
            mChildTiles.Clear();
        }

        //销毁当前Node
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
