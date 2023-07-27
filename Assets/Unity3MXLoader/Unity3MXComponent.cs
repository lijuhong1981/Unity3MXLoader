using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json.Linq;

/**
 * Unity3MXLoader
 * һ������3MXģ�͵�Unity���
 * @author lijuhong1981
 */
namespace Unity3MX
{
    public class Unity3MXComponent : MonoBehaviour
    {
        [Tooltip("��ʼ����url")]
        public string url;
        [Tooltip("���������������ʹ��Camera.main")]
        public Camera mainCamera;
        [Tooltip("�Ƿ���ű�����ִ��")]
        public bool runOnStart = true;
        [Tooltip("ִ�д����߼�ʱ��������λ��")]
        [Min(0)]
        public float processInterval = 1.0f / 30.0f;
        [Tooltip("��󲢷���������Ҳ�������Ϊ��󲢷��߳���")]
        [Min(1)]
        public int maxTaskCount = Environment.ProcessorCount;
        [Tooltip("ֱ�����ű��ʣ���Ӱ�쵱ǰ�����ͼ����ʾ�㼶��ѡ��")]
        [Min(0.1f)]
        public float diameterRatio = 1.0f;
        [Tooltip("���fov���ʣ���Ӱ������ɼ�����ķ�Χ")]
        [Min(0.1f)]
        public float fieldOfViewRatio = 1.2f;
        [Tooltip("ģ�Ͳ�����ʹ�õ�Shader")]
        public string shaderName = "HDRP/Lit";
        [Tooltip("��Ӱģʽ")]
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        [Tooltip("ʧ�����Դ���")]
        [Min(0)]
        public int failRetryCount = 5;
        [Tooltip("�Ƿ�����DebugCollider���������Ϊÿ��Tile���һ��BoxCollider")]
        public bool enableDebugCollider = false;
        [Tooltip("�Ƿ����ڴ滺�棬��������ع�����Ƭ��Դ�Ỻ�����ڴ���ܼ��ټ��ش�������Ӵ��ڴ�����")]
        public bool enableMemeoryCache = false;

        private float mLastTime = 0.0f;
        private CameraState mCameraState;

        private string mBaseUrl;
        public string baseUrl
        {
            get { return mBaseUrl; }
        }

        private bool mLoading = false;
        public bool isLoading
        {
            get { return mLoading; }
        }

        private bool mReady = false;
        public bool isReady
        {
            get { return mReady; }
        }

        private string mSRS;
        public string SRS
        {
            get { return mSRS; }
        }

        private double[] mSRSOrigin;
        public double[] SRSOrigin
        {
            get { return mSRSOrigin; }
        }

        private string mRootDataPath;
        public string rootDataPath
        {
            get { return mRootDataPath; }
        }

        private string mBaseDataUrl;
        public string baseDataUrl
        {
            get { return mBaseDataUrl; }
        }

        private bool mHasOffset = false;
        private Vector3 mOffset = new();
        public Vector3 offset
        {
            get { return mOffset; }
        }

        private GameObject mRootObject;
        public GameObject rootObject
        {
            get { return mRootObject; }
        }

        private List<Unity3MXRootNode> mRootNodes = new();
        public List<Unity3MXRootNode> rootNodes
        {
            get { return mRootNodes; }
        }
        private List<Unity3MXTileNode> mReadyUnloadNodes = new();

        public delegate void OnReady(Unity3MXComponent component);
        public event OnReady onReady;
        public delegate void OnError(Unity3MXComponent component, string error);
        public event OnError onError;

        internal TaskPool taskPool = new();

        // Start is called before the first frame update
        void Start()
        {
            Debug.Log("ProcessorCount: " + Environment.ProcessorCount);
            if (mainCamera == null)
                mainCamera = Camera.main;
            if (runOnStart)
                Run();
        }

        private void OnDestroy()
        {
            taskPool.Dispose();
            if (mCameraState != null)
                mCameraState.Destroy();
        }

        // Update is called once per frame
        void Update()
        {

        }

        void LateUpdate()
        {
            //������ʱ���Ƿ����updateIntervalTime�������������ִ��
            float nowTime = Time.time;
            if (nowTime - mLastTime < processInterval)
                return;
            mLastTime = nowTime;
            //�������Ϊ��
            if (mainCamera != null)
            {
                //����CameraState��ִ��Update
                if (mCameraState == null)
                    mCameraState = new CameraState();
                mCameraState.Update(this, mainCamera);
            }
            //δ��ʼ����δ��ȡ���������������ִ��
            if (!mReady || mCameraState == null || mRootNodes.Count == 0)
                return;
            //����offset������ƫ��
            if (mHasOffset)
                mRootObject.transform.localPosition = mOffset;

            taskPool.MaxTasks = maxTaskCount;

            //�����������������
            mRootNodes.Sort(compareRootNode);
            //ִ��node.Update
            foreach (var rootNode in mRootNodes)
            {
                rootNode.Process(mCameraState);
            }
            //test code
            //var length = Math.Min(2, mRootNodes.Count);
            //for (int i = 0; i < length; i++)
            //{
            //    mRootNodes[i].Update(mCameraState);
            //}
            //mRootNodes[1].Update(mCameraState);

            if (mReadyUnloadNodes.Count > 0)
            {
                //����readyUnloadNodes
                foreach (var node in mReadyUnloadNodes)
                {
                    //ִ��node.Unload
                    node.Unload();
                }
                //ִ��������readyUnloadNodes
                mReadyUnloadNodes.Clear();
            }
        }

        private int compareRootNode(Unity3MXRootNode left, Unity3MXRootNode right)
        {
            var result = left.cameraDistance - right.cameraDistance;
            if (result > 0)
                return Mathf.CeilToInt(result);
            else if (result < 0)
                return Mathf.FloorToInt(result);
            return 0;
        }

        public void Run()
        {
            //�Ѽ�����ɣ���Ҫ�ȵ���Clear()������ٴμ���
            if (mReady)
                return;
            //���ڼ����У������ظ�����
            if (mLoading)
                return;
            //���url�Ƿ����
            if (UrlUtils.CheckUrl(url, (string error) =>
            {
                mLoading = false;
                onError?.Invoke(this, error);
            }))
            {
                //��ȡbaseUrl
                mBaseUrl = UrlUtils.ExtractBaseUrl(url);
                Debug.Log("Get baseUrl: " + mBaseUrl);
                //����TaskPool
                taskPool.Start();
                //��ʼ��ʼ��
                StartCoroutine(initialize());
            }
        }

        //��ʼ��
        private IEnumerator initialize()
        {
            mLoading = true;

            bool isError = false;
            //��ȡurlָ����ı�
            yield return RequestUtils.GetText(url, (string error) =>
            {
                onError?.Invoke(this, error);
                isError = true;
            }, (string text) =>
            {
                Debug.Log("Get rootJson: " + text);

                parseRootJson(text);
            });

            if (isError)
            {
                mLoading = false;
                yield break;
            }

            yield return createRootObject();
            yield return loadRootData();
        }

        //����Json�ı�
        private void parseRootJson(string jsonText)
        {
            JObject jRoot = JObject.Parse(jsonText);
            JArray jLayers = (JArray)jRoot.GetValue("layers");
            JObject jLayer = (JObject)jLayers[0];

            mSRS = (string)jLayer.GetValue("SRS");

            JArray jSRSOrigin = (JArray)jLayer.GetValue("SRSOrigin");
            mSRSOrigin = new double[jSRSOrigin.Count];
            for (int i = 0; i < jSRSOrigin.Count; i++)
            {
                mSRSOrigin[i] = (double)jSRSOrigin[i];
            }

            string rootPath = (string)jLayer.GetValue("root");
            mRootDataPath = mBaseUrl + rootPath;
            Debug.Log("Get rootDataPath: " + mRootDataPath);
            mBaseDataUrl = UrlUtils.ExtractBaseUrl(mRootDataPath);

            JToken value = jLayer.GetValue("offset");
            if (value != null)
            {
                JArray jOffset = (JArray)value;
                if (jOffset[0] != null)
                    mOffset.x = (float)jOffset[0];
                if (jOffset[1] != null)
                    mOffset.y = (float)jOffset[2];
                if (jOffset[2] != null)
                    mOffset.z = (float)jOffset[1];
                mHasOffset = true;
            }
        }

        //����������
        private IEnumerator createRootObject()
        {
            mRootObject = new GameObject("Root");
            mRootObject.transform.SetParent(this.transform, false);
            yield return new WaitForEndOfFrame();
        }

        //���ظ�����
        private IEnumerator loadRootData()
        {
            Unity3MXBLoader loader = new Unity3MXBLoader(this, mRootDataPath);
            loader.onLoad += (HeaderInfo header) =>
            {
                //���ɸ��ڵ�
                for (int i = 0; i < header.nodes.Count; i++)
                {
                    NodeInfo node = header.nodes[i];
                    mRootNodes.Add(new Unity3MXRootNode(this, node));
                }
                //��ʼ�����
                mLoading = false;
                mReady = true;
                Debug.Log("This 3MXComponent is ready.");
                onReady?.Invoke(this);
            };
            loader.onError += (string error) =>
            {
                onError?.Invoke(this, error);
            };
            loader.Load();
            yield return null;
        }

        public void Clear()
        {
            //����δ��ɣ����ܵ���Clear()
            if (!mReady)
                return;

            taskPool.Clear();
            taskPool.Stop();
            //StopAllCoroutines();

            foreach (var node in mRootNodes)
            {
                node.Destroy();
            }
            mRootNodes.Clear();
            mReadyUnloadNodes.Clear();
            if (mRootObject != null)
            {
                Destroy(mRootObject);
                mRootObject = null;
            }

            mReady = false;
        }

        internal void AddReadyUnloadNode(Unity3MXTileNode node)
        {
            mReadyUnloadNodes.Add(node);
        }

        //���Node�ĸ�Node�Ƿ���ready״̬���������readyUnloadNodes�б����Ƴ�
        internal void CheckAndRemoveParentNode(Unity3MXTileNode node)
        {
            var parentNode = node.tile.parentNode;
            if (parentNode != null)
            {
                //�ж�parentNode�Ƿ���ready�������readyUnloadNodes�б����Ƴ�
                if (parentNode.isReady)
                    mReadyUnloadNodes.Remove(parentNode);
                //����������ϵݹ����
                else
                    CheckAndRemoveParentNode(parentNode);
            }
        }
    }

    public class CameraState
    {
        public Camera camera;
        private Camera mCamera;
        public Plane[] planes;
        public float fieldOfView;
        public float nearClipPlane;
        public float inverseNear;
        public float topClipPlane;
        public float rightClipPlane;

        public void Update(Unity3MXComponent rootComponent, Camera camera)
        {
            bool changed = false;
            if (mCamera != camera)
            {
                mCamera = camera;
                if (this.camera != null)
                    UnityEngine.Object.Destroy(this.camera);
                //����һ�������
                var gameObject = new GameObject("Unity3MXCamera");
                this.camera = gameObject.AddComponent<Camera>();
                this.camera.orthographic = mCamera.orthographic;
                this.camera.transform.SetParent(mCamera.transform.parent);
                //����Ϊ�ǻ״̬
                this.camera.gameObject.SetActive(false);
                changed = true;
            }
            if (this.fieldOfView != camera.fieldOfView)
            {
                this.fieldOfView = camera.fieldOfView;
                changed = true;
            }
            if (this.nearClipPlane != camera.nearClipPlane)
            {
                this.nearClipPlane = camera.nearClipPlane;
                //����1.0��nearClipPlane��ֵ
                this.inverseNear = 1.0f / this.nearClipPlane;
                changed = true;
            }
            if (changed)
            {
                //����topClipPlane
                this.topClipPlane = this.nearClipPlane * Mathf.Tan(0.5f * this.fieldOfView * Mathf.Deg2Rad);
                //����rightClipPlane
                this.rightClipPlane = camera.aspect * this.topClipPlane;
            }
            //���������ز���
            this.camera.fieldOfView = camera.fieldOfView * rootComponent.fieldOfViewRatio;
            this.camera.aspect = camera.aspect;
            this.camera.nearClipPlane = camera.nearClipPlane;
            this.camera.farClipPlane = camera.farClipPlane;
            this.camera.transform.position = camera.transform.position;
            this.camera.transform.rotation = camera.transform.rotation;
            //��ȡ�����׶��planes
            this.planes = GeometryUtility.CalculateFrustumPlanes(this.camera);
        }

        //�����Ƿ�ɼ�
        public bool TestVisibile(Bounds bounds)
        {
            return GeometryUtility.TestPlanesAABB(this.planes, bounds);
        }

        //���㵥λ���ش�С������/����
        public float ComputePixelSize(Vector3 position)
        {
            float distance = Vector3.Distance(position, this.camera.transform.position);
            float tanTheta = this.topClipPlane * this.inverseNear;
            float pixelHeight = (2.0f * distance * tanTheta) / Screen.height;
            tanTheta = this.rightClipPlane * this.inverseNear;
            float pixelWidth = (2.0f * distance * tanTheta) / Screen.width;
            return Mathf.Max(pixelWidth, pixelHeight);
        }

        public void Destroy()
        {
            if (this.camera != null)
                UnityEngine.Object.Destroy(this.camera);
            this.camera = null;
            mCamera = null;
        }
    }

}
