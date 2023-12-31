using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/**
 * Unity3MXLoader
 * 一个加载3MX模型的Unity插件
 * @author lijuhong1981
 */
namespace Unity3MX
{
    public class UrlUtils
    {
        public static bool IsLegalUrl(string url)
        {
            if (url == null || url.Length == 0)
                return false;
            return true;
        }

        public static bool CheckUrl(string url, Action<string> onError = null)
        {
            if (!IsLegalUrl(url))
            {
                string error = "This url " + url + " is illeagl.";
                Debug.LogError(error);
                onError?.Invoke(error);
                return false;
            }
            return true;
        }

        public static string ExtractBaseUrl(string url)
        {
            Uri uri = new Uri(url);
            string noLastSegment = "";
            for (int i = 0; i < uri.Segments.Length - 1; i++)
            {
                noLastSegment += uri.Segments[i];
            }
            noLastSegment.TrimEnd('/');
            UriBuilder builder = new UriBuilder(uri);
            builder.Path = noLastSegment;
            //builder.Query = null;
            //builder.Fragment = null;
            return builder.Uri.ToString();
        }

        public static string ExtractFileName(string url)
        {
            Uri uri = new Uri(url);
            var lastSegment = UnityWebRequest.UnEscapeURL(uri.Segments[uri.Segments.Length - 1]);
            var end = lastSegment.LastIndexOf("?");
            string result;
            if (end == -1)
                result = lastSegment;
            else
                result = lastSegment.Substring(0, end);
            return result;
        }
    }

    public class RequestUtils
    {
        public static IEnumerator Get(string url, Action<string> onError = null, Action<string> onText = null, Action<byte[]> onData = null)
        {
            if (UrlUtils.CheckUrl(url, onError))
            {
                if (url.StartsWith("file:///"))
                {
                    //本地磁盘路径(中文编码转换)
                    url = "file:///" + UnityWebRequest.EscapeURL(url.Substring(8));
                }
                else if (url.StartsWith("file://"))
                {
                    //共享文件夹
                    url = "file:////" + url.Substring(7);
                }
                var time = Time.time;
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    yield return www.SendWebRequest();
                    time = Time.time - time;
                    Debug.Log("This request " + url + " use time: " + time);

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("Get " + url + " error: " + www.error);
                        onError?.Invoke(www.error);
                    }
                    else
                    {
                        onText?.Invoke(www.downloadHandler.text);
                        onData?.Invoke(www.downloadHandler.data);
                    }
                }
            }
        }

        public static IEnumerator GetText(string url, Action<string> onError, Action<string> onText)
        {
            yield return Get(url, onError, onText, null);
        }

        public static IEnumerator GetData(string url, Action<string> onError, Action<byte[]> onData)
        {
            yield return Get(url, onError, null, onData);
        }
    }
}
