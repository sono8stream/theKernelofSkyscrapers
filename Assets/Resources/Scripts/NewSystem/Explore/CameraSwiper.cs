﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraSwiper : MonoBehaviour
{
    #region Property
    [SerializeField]
    Camera camera;
    [SerializeField]
    MapLoader map;
    [SerializeField]
    PanelMenu panelMenu;
    [SerializeField]
    RobotMenu robotMenu;
    [SerializeField]
    GameObject panelOrigin;
    [SerializeField]
    GameObject dammySprite;
    [SerializeField]
    GameObject kernel;
    MapObject selectedObject;
    Vector3 keyDownPos;
    Vector3 touchingPos;
    Vector3 tappedMapPos;
    Vector3 mapCorrectionPos;//カメラ→マップ座標系変化時の補正
    Vector3 velocity;
    Vector3 accel;
    bool cameraIsFixing;//カメラ固定状態
    public bool onPanel;
    int floorNo;
    #region for Scroll
    float swipeMargin;
    float period;//スクロール時間
    float speed = 0.1f;
    float marginY = -8;
    float rangeX;
    float rangeY;
    float correctionDown = /*2*/16;
    float posZ;
    #endregion
    #endregion

    // Use this for initialization
    void Start()
    {
        swipeMargin = 5;
        mapCorrectionPos = Vector3.back * 0.01f - camera.transform.localPosition;
        period = 80;
        rangeX = map.MapWidth * 0.5f;
        rangeY = map.MapHeight * 0.5f + marginY;
        posZ = camera.transform.localPosition.z;
        camera.transform.localPosition = kernel.transform.position - mapCorrectionPos;
    }

    // Update is called once per frame
    void Update()
    {
        if (selectedObject != null)//ステータス表示中、ロボを追従
        {
            RectTransform canvasRect = GetComponent<RectTransform>();
            /*transform.FindChild("SelectingRobot").GetComponent<RectTransform>().anchoredPosition
                = SetToCanvasPos(selectedObject.transform.position);*/
        }
        if (velocity != Vector3.zero)//余韻スクロール
        {
            camera.transform.localPosition += velocity;
            LimitScroll(map.MapWidth, map.MapHeight, false);
            velocity += accel;
        }
        //Debug.Log(camera.ViewportToWorldPoint(Input.mousePosition));
        dammySprite.transform.position = SetToMapPos();
    }


    public void TouchDownScreen()
    {
        keyDownPos = Input.mousePosition;
        touchingPos = keyDownPos;
    }

    public void TouchingScreen()
    {
        camera.transform.localPosition += (touchingPos - Input.mousePosition) / period * 1.5f;
        LimitScroll(map.MapWidth, map.MapHeight, false);
        touchingPos = Input.mousePosition;
    }

    public void TouchUpScreen()
    {
        Vector3 posTemp = Input.mousePosition;
        if (keyDownPos.x - swipeMargin < posTemp.x && posTemp.x < keyDownPos.x + swipeMargin
            && keyDownPos.y - swipeMargin < posTemp.y && posTemp.y < keyDownPos.y + swipeMargin)
        {
            tappedMapPos = camera.ScreenToWorldPoint(touchingPos) + mapCorrectionPos;
            //aTapPoint = new Vector3(aTapPoint.x, aTapPoint.y, 0);
            Collider[] aCollider = Physics.OverlapSphere(tappedMapPos, 0.4f);
            foreach (Collider col in aCollider)
            {
                if (col && col.tag == "Robot")
                {
                    selectedObject = col.gameObject.GetComponent<MapObject>();
                }
            }
            Debug.Log(tappedMapPos);
            CellData c = map.GetMapData(floorNo, tappedMapPos);
            if (onPanel && 0 <= panelMenu.PanelNo/*パネル生成*/&& c != null && c.panelNo == -1)
            {
                GameObject g = Instantiate(panelOrigin);
                g.GetComponent<Panel>().command = Data.commands[panelMenu.PanelNo].CreateInstance();
                g.transform.position = dammySprite.transform.position;
                g.transform.localScale = Vector3.one;
                map.SetPanelData(floorNo, tappedMapPos, panelMenu.PanelNo);
            }
            else if (!onPanel && 0 <= robotMenu.RobotNo // ロボ生成
                && c != null
                && c.objNo == -1
                && c.tile.activeSelf)
            {
                GameObject g = Instantiate(robotMenu.robotOrigin);
                RobotController rc = g.GetComponent<RobotController>();
                rc.Robot = (Robot)UserData.instance.robotRecipe[robotMenu.RobotNo].DeepCopy();
                rc.Robot.Initiate();
                Debug.Log(rc.Robot.Command.Count);
                g.transform.position = dammySprite.transform.position;
                g.transform.localScale = Vector3.one;
            }
        }
        else
        {
            velocity = (touchingPos - posTemp) / period;
            accel = -velocity / 10;
        }
        //SetStatus(selectedObject);
        LimitScroll(map.MapWidth, map.MapHeight);
    }

    //カメラのスクロール限界
    void LimitScroll(int sizeX, int sizeY, bool bound = true)
    {
        if (camera.transform.localPosition.x <  - rangeX)
        {
            camera.transform.position = new Vector3( - rangeX, camera.transform.localPosition.y, posZ);
            if (bound)
            {
                velocity.x = speed;
            }
            accel = velocity / (-10);
        }
        if (camera.transform.localPosition.x > rangeX)
        {
            camera.transform.localPosition
                = new Vector3(rangeX, camera.transform.localPosition.y, posZ);
            if (bound)
            {
                velocity.x = -speed;
            }
            accel = velocity / (-10);
        }
        if (camera.transform.localPosition.y < -(rangeY + correctionDown))
        {
            camera.transform.localPosition 
                = new Vector3(camera.transform.localPosition.x, -(rangeY + correctionDown), posZ);
            if (bound)
            {
                velocity.y = speed;
            }
            accel = velocity / (-10);
        }
        if (camera.transform.localPosition.y > rangeY)
        {
            camera.transform.localPosition = new Vector3(camera.transform.localPosition.x, rangeY, posZ);
            if (bound)
            {
                velocity.y = -speed;
            }
            accel = velocity / (-10);
        }
        //setDire.GetComponent<RectTransform>().anchoredPosition = SetToScreenPos(setPos + Vector2.down);
    }

    Vector3 SetToMapPos()
    {
        float centerX = camera.rect.width / 2 - 0.5f;
        float cameraAngle = 360 - camera.transform.eulerAngles.x;
        float angleX, angleZ;
        float aspectRatio;
        Vector2 posView = camera.ScreenToViewportPoint(Input.mousePosition)
            - new Vector3(/*camera.rect.width */ 0.5f, 0.5f);
        Vector2 screenSize = camera.ViewportToScreenPoint(Vector3.one);
        angleX = (cameraAngle + camera.fieldOfView * posView.y) * Mathf.PI / 180;
        aspectRatio = screenSize.x / screenSize.y;
        angleZ = camera.fieldOfView * aspectRatio * posView.x * Mathf.PI / 180;
        Vector3 targetPos = camera.transform.position
            + new Vector3(mapCorrectionPos.z * Mathf.Tan(angleZ) / Mathf.Cos(angleX),
            mapCorrectionPos.z * Mathf.Tan(angleX), mapCorrectionPos.z);
        targetPos = new Vector3(Mathf.Round(targetPos.x), Mathf.Round(targetPos.y), targetPos.z);
        return targetPos;
    }

    Vector2 SetToCanvasPos(Vector2 pos)
    {
        RectTransform canvasRect = GetComponent<RectTransform>();
        Vector2 viewportPosition = camera.WorldToViewportPoint(pos);
        Vector2 worldObject_ScreenPosition = new Vector2(
            ((viewportPosition.x * canvasRect.sizeDelta.x) - (canvasRect.sizeDelta.x * 0.5f)),
            ((viewportPosition.y * canvasRect.sizeDelta.y) - (canvasRect.sizeDelta.y * 0.5f)));
        return worldObject_ScreenPosition;
    }
}