using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity3MX
{
    public class Unity3MXRootNode
    {
        private Unity3MXComponent mRootComponent;
        private NodeInfo mNodeInfo;
        public NodeInfo nodeInfo
        {
            get { return mNodeInfo; }
        }
        public string id
        {
            get { return mNodeInfo.id; }
        }
        private string mBaseUrl;
        public string baseUrl
        {
            get { return mBaseUrl; }
        }
        private List<Unity3MXTile> mChildTiles = new();
        private GameObject mGameObject;
        public GameObject gameObject
        {
            get { return mGameObject; }
        }
        public bool isReady
        {
            get { return mGameObject; }
        }
        private BoxCollider mCollider;
        private float mCameraDistance;
        public float cameraDistance
        {
            get { return mCameraDistance; }
        }
        private bool mDestroyed = false;
        public bool isDestroyed
        {
            get { return mDestroyed; }
        }

        public Unity3MXRootNode(Unity3MXComponent rootComponent, NodeInfo nodeInfo)
        {
            mRootComponent = rootComponent;
            mNodeInfo = nodeInfo;
            //获取baseUrl
            mBaseUrl = rootComponent.baseDataUrl + nodeInfo.id + "/";
        }

        public void Update(CameraState cameraState)
        {
            if (mDestroyed)
                return;
            //gameObject为null，说明该Node还未初始化
            if (mGameObject == null)
            {
                //生成gameObject
                mGameObject = new GameObject(mNodeInfo.id);
                //生成BoxCollider，用于测试是否可见
                mCollider = mGameObject.AddComponent<BoxCollider>();
                mCollider.center = mNodeInfo.bounds.center;
                mCollider.size = mNodeInfo.bounds.size;
                //挂载到rootObject下
                mGameObject.transform.SetParent(mRootComponent.rootObject.transform, false);
                //计算当前Node中心点到相机位置的距离
                mCameraDistance = Vector3.Distance(mCollider.bounds.center, cameraState.camera.transform.position);
                //完成后直接return，等待下一帧对RootNode排序后再执行下面的代码
                return;
            }
            //计算当前Node中心点到相机位置的距离
            mCameraDistance = Vector3.Distance(mCollider.bounds.center, cameraState.camera.transform.position);
            //判断Node是否可见
            if (cameraState.TestVisibile(mCollider.bounds))
            {
                //Node可见，调用ensureChildTiles
                ensureChildTiles();
                //执行tile.Update
                foreach (var tile in mChildTiles)
                {
                    tile.Update(cameraState);
                }
            }
            else
            {
                //Node不可见，enableMemeoryCache为true时只卸载不销毁
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
        }

        //判断子Tile是否已创建，未创建则创建
        private void ensureChildTiles()
        {
            if (mChildTiles.Count == 0)
            {
                for (int i = 0; i < mNodeInfo.children.Count; i++)
                {
                    mChildTiles.Add(new Unity3MXTile(mRootComponent, this, mNodeInfo.children[i]));
                }
            }
        }

        //销毁gameObject
        private void destroyGameObject()
        {
            if (mGameObject != null)
            {
                Object.Destroy(mGameObject);
                mGameObject = null;
            }
        }

        //卸载所有子Tile
        public void UnloadChildren()
        {
            foreach (var tile in mChildTiles)
            {
                tile.Unload(true);
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

        //销毁
        public void Destroy()
        {
            if (mDestroyed)
                return;

            DestroyChildren();

            destroyGameObject();

            mDestroyed = true;
        }
    }
}
