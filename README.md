# Unity3MXLoader
 一个加载3MX模型的Unity插件
 
 ## 使用
 添加Unity3MXComponent脚本至GameObject对象下即可
 
 ## Unity3MXComponent参数说明
 * url：3mx模型文件地址
 * mainCamera：主相机对象，不设置则默认使用Camera.main
 * runOnStart：是否脚本启动即加载
 * processInterval：Unity3MXComponent内部逻辑执行间隔时间，单位秒
 * maxTaskCount：最大并发任务数，也可以理解为最大并发线程数
 * diameterRatio：3MX模型节点像素投影直径大小的缩放比值，会影响当前相机视图下显示层级的选择
 * fieldOfViewRatio：相机fov倍率，会影响相机可见区域的范围
 * shaderName：模型材质所使用的Shader设置
 * shadowCastingMode：阴影设置
 * failRetryCount：文件加载失败后的最大重试次数
 * enableMemeoryCache：是否开启内存缓存，开启后加载过的瓦片资源会缓存在内存里，能减少加载次数但会加大内存消耗
 * isReady[ReadOnly]：判断是否已初始化完成
 * onReady[event]：准备完成事件通知
 * onError[event]：加载出错事件通知
 
 ## Unity3MXComponent函数说明
 * Run()：开始加载
 * Clear()：清除掉已加载的数据，只有isReady为true时可调用
 
 ## Thirdparty
 [OpenCTM-Unity]https://github.com/unity-car-tutorials/OpenCTM-Unity
