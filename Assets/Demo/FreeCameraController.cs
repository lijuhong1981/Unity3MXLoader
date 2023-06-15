using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FreeCameraController : MonoBehaviour
{
    [Tooltip("Mouse scroll control zoom in and out")]
    public bool ZoomEnable = true;

    [Tooltip("Left mouse button controls movement")]
    public bool MoveEnable = true;

    [Tooltip("Right mouse button control rotation")]
    public bool RoateEnable = true;

    //绕模型中心旋转、缩放
    public Transform rotateCenter;

    //鼠标缩放距离最值
    public float MaxDistance = 9000;
    public float MinDistance = 1.5F;
    //鼠标缩放速率
    public float ZoomSpeed = 2F;

    float distance;
    //速度
    public float Damping = 10F;

    [HideInInspector]
    public bool isOnPOICanvas = false;
    [HideInInspector]
    public bool AxisEnable = false;

    bool leftMouseClick = false;
    bool rightMouseClick = false;
    bool canScroll = true;

    //临时旋转、位置记录
    private Quaternion mRotation;
    private Vector3 mPosition = Vector3.zero;
    private Vector3 cPosition = Vector3.zero;

    //旋转速度
    public float SpeedX = 240;
    public float SpeedY = 120;
    //旋转角度
    private float rX = 0.0F;
    private float rY = 0.0F;
    //角度限制
    public float MinLimitY = -90;
    public float MaxLimitY = 90;

    //移动距离
    private float mX = 0.0F;
    private float mY = 0.0F;

    //public Unity3mxComponent unity3MxComponent;
    private Vector3 camPos;
    private Quaternion camRot;

    private void Awake()
    {
        rX = transform.eulerAngles.y;
        rY = transform.eulerAngles.x;
        mRotation = transform.rotation;
        mPosition = transform.position;
        if (rotateCenter)
        {
            distance = (transform.position - rotateCenter.position).magnitude;
        }
    }

    void Update()
    {
        if (AxisEnable)
            return;

        //前进后退
        if (ZoomEnable && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                canScroll = false;
            }
            else
            {
                canScroll = true;
            }

            if (canScroll || isOnPOICanvas)
            {

                if (rotateCenter)
                {
                    distance -= Input.GetAxis("Mouse ScrollWheel") * ZoomSpeed;
                    distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
                    mPosition = mRotation * new Vector3(0.0F, 0.0F, -distance) + rotateCenter.position;
                    transform.position = Vector3.Lerp(transform.position, mPosition, Time.deltaTime * Damping);
                }
                else
                {
                    mPosition = mRotation * new Vector3(0.0F, 0.0F, Input.GetAxis("Mouse ScrollWheel") * ZoomSpeed) + transform.position;
                    transform.position = Vector3.Lerp(transform.position, mPosition, Time.deltaTime * Damping);
                }
            }
        }

        //鼠标移动事件最原始在UI上不起作用
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject() && !isOnPOICanvas)
            {
                leftMouseClick = false;
            }
            else
            {
                leftMouseClick = true;
                lastLeftMousePos = Input.mousePosition;
                mPosition = transform.position;
                //unity3MxComponent.enabled = false;
            }
        }

        //移动视角
        if (Input.GetMouseButton(0))
        {
            if (MoveEnable && leftMouseClick)
            {
                GetLeftMouseAxisXY();

                if(leftMouseAxisXY.x==0&& leftMouseAxisXY.y == 0)
                {
                    return;
                }

                mX = leftMouseAxisXY.x * SpeedX * 5F;
                mY = leftMouseAxisXY.y * SpeedY * 5F;

                cPosition = transform.right * -mX + transform.up * -mY + mPosition;

                transform.position = Vector3.Lerp(transform.position, cPosition, Time.deltaTime * Damping);

                mPosition = cPosition;
                //unity3MxComponent.enabled = true;
            }
        }

        //鼠标旋转事件最原始在UI上不起作用
        if (Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current.IsPointerOverGameObject() && !isOnPOICanvas)
            {
                rightMouseClick = false;
            }
            else
            {
                rightMouseClick = true;
                lastRightMousePos = Input.mousePosition;
                //unity3MxComponent.enabled = false;
            }
        }

        //旋转视角
        if (RoateEnable && Input.GetMouseButton(1))
        {
            if (RoateEnable && rightMouseClick)
            {
                GetRightMouseAxisXY();

                rX += rightMouseAxisXY.x * SpeedX;
                rY -= rightMouseAxisXY.y * SpeedY;

                rY = ClampAngle(rY, MinLimitY, MaxLimitY);

                mRotation = Quaternion.Euler(rY, rX, 0);

                transform.rotation = Quaternion.Lerp(transform.rotation, mRotation, Time.deltaTime * Damping);

                if (rotateCenter)
                {
                    distance = (transform.position - rotateCenter.position).magnitude;
                    mPosition = mRotation * new Vector3(0.0F, 0.0F, -distance) + rotateCenter.position;
                    transform.position = Vector3.Lerp(transform.position, mPosition, Time.deltaTime * Damping);
                }
                //unity3MxComponent.enabled = true;
            }
        }

        //if (Camera.main.transform.position == camPos && Camera.main.transform.rotation == camRot)
        //{
        //    unity3MxComponent.enabled = true;
        //}
        //else
        //{
        //    unity3MxComponent.enabled = false;
        //    camPos = Camera.main.transform.position;
        //    camRot = Camera.main.transform.rotation;
        //}
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360)
        {
            angle += 360;
        }
        if (angle > 360)
        {
            angle -= 360;
        }
        return Mathf.Clamp(angle, min, max);
    }

    Vector3 lastLeftMousePos = Vector3.zero;
    Vector2 leftMouseAxisXY = Vector2.zero;

    private void GetLeftMouseAxisXY()
    {
        var offset = Input.mousePosition - lastLeftMousePos;
        leftMouseAxisXY.x = offset.x / Screen.width;
        leftMouseAxisXY.y = offset.y / Screen.height;

        lastLeftMousePos = Input.mousePosition;
    }

    Vector3 lastRightMousePos = Vector3.zero;
    Vector2 rightMouseAxisXY = Vector2.zero;

    private void GetRightMouseAxisXY()
    {
        var offset = Input.mousePosition - lastRightMousePos;
        rightMouseAxisXY.x = offset.x / Screen.width;
        rightMouseAxisXY.y = offset.y / Screen.height;

        lastRightMousePos = Input.mousePosition;
    }

}
