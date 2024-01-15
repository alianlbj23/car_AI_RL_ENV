using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using Math = System.Math;
using System.Reflection;
using WebSocketSharp;
using MiniJSON;
using UnityEngine.SceneManagement;

public class TrainingManager : MonoBehaviour
{
    string Unity2AI_Topic = "/Unity_2_AI";
    string AI2Unity_Receive_Topic = "/AI_2_Unity";
    string Unity2AI_action_finish_Topic = "/Unity_2_AI_RESET_flag";
    string AI2Unity_reset_Topic = "/AI_2_Unity_RESET_flag";
    string AI2Unity_action_reset_Topic = "/AI_2_Unity_ACTION_RESET_flag";
    
    private WebSocket socket;
    private string rosbridgeServerUrl = "ws://localhost:9090";
    public Robot robot;
    [SerializeField]
    GameObject anchor1, anchor2, anchor3, anchor4;
    Vector3[] outerPolygonVertices;
    [SerializeField]
    GameObject target;
    Vector3 newTarget_car;

    Vector3 newTarget;
    public System.Random random = new System.Random();
    Transform baselink;
    // Transform baselink;
    [System.Serializable]
    public class RobotNewsMessage
    {
        public string op;
        public string topic;
        public MessageData msg;
    }
    [System.Serializable]
    public class MessageData
    {
        public LayoutData layout;
        public float[] data;
    }
    [System.Serializable]
    public class LayoutData
    {
        public int[] dim;
        public int data_offset;
    }

    enum Phase
    {
        Freeze,
        Run
    }
    Phase phase;
    public float stepTime = 0.05f; //0.1f
    public float currentStepTime = 0.0f;
    float target_change_flag = 0;
    string mode = "Training";
    void Awake()
    {
        baselink = robot.transform.Find("base_link");
    }

    Vector3 carPos;
    
    float key = 0;
    public float delayInSeconds = 0f;
    void Start()
    {
        StartCoroutine(DelayedExecution());
    }

    IEnumerator DelayedExecution()
    {
        delayInSeconds = 0.001f;
        yield return new WaitForSeconds(delayInSeconds);
        
        
        newTarget = GetTargetPosition(target, newTarget);

        socket = new WebSocket(rosbridgeServerUrl);
        

        socket.OnOpen += (sender, e) =>
        {
            SubscribeToTopic(AI2Unity_Receive_Topic);
            SubscribeToTopic(AI2Unity_reset_Topic);
        };
        socket.OnMessage += OnWebSocketMessage;


        socket.Connect();

        // change_target();
        get_position_test();
        
        // State state = robot.GetState(newTarget);

        // Send(state);
    }

    float target_x;
    float target_y;
    float target_x_car;
    float target_y_car;

    
    float delayTimer = 0.0f;
    float delayDuration = 2.0f;
    bool isDelayedActionTriggered = false;
    void FixedUpdate ()
    {
        if (mode == "Training")
        {
            if (key == 1)
            {
                    // State state = robot.GetState(newTarget);
                    // Send(state);
                    key = 0;
                    StartCoroutine(DelayedSend(0.7f, newTarget));
                
                // actionFinish();
                
            }


            if (target_change_flag == 1)
            {
                ReloadCurrentScene();
            }
        }
        
        
        
        else // 這邊是testing用
        {
            CarMove();
            if (target_change_flag == 1){
                change_target();
                if (!isDelayedActionTriggered)
                {
                    delayTimer += Time.deltaTime;
                    if (delayTimer >= delayDuration)
                    {
                        // State state = updateState(newTarget);
                        // Debug.Log(state);
                        // Send(state);                        
                    }
                }
                target_change_flag = 0;
            }
            // else if(target_change_flag == 2){
            //     if (!isDelayedActionTriggered)
            //     {
            //         delayTimer += Time.deltaTime;
            //         if (delayTimer >= delayDuration)
            //         {
            //             State state = updateState(newTarget);
            //             Send(state);
            //             Debug.Log("reset!!!!!");
            //             isDelayedActionTriggered = true;
            //         }
            //         target_change_flag = 0;
            //     }
            // }
            
        }
        
    }
    IEnumerator DelayedSend(float delayTime, Vector3 newTarget)
    {
        // 等待一段时间
        yield return new WaitForSeconds(delayTime);

        // 延迟后执行的代码
        State state = robot.GetState(newTarget);
        Send(state);
        key = 0;

        // 如果需要的话，这里可以调用actionFinish或其他函数
        // actionFinish();
    }

    void ReloadCurrentScene()
        {
            // 获取当前场景的名称
            string currentSceneName = SceneManager.GetActiveScene().name;

            // 重新加载当前场景
            SceneManager.LoadScene(currentSceneName);
        }

    void StartStep()
    {
        phase = Phase.Run;
        currentStepTime = 0;
        Time.timeScale = 1;
    }

    void EndStep()
    {
        phase = Phase.Freeze;
        State state = updateState(newTarget);
        Send(state);
    }


    void MoveGameObject(GameObject obj, Vector3 pos)
    {
        obj.transform.position = pos;
    }

    void MoveRobot(Vector3 pos)
    {
        baselink.GetComponent<ArticulationBody>().TeleportRoot(pos, Quaternion.identity);
    }


    Vector3 GetTargetPosition(GameObject obj, Vector3 pos)  //  取得target position
    {
        Transform objTransform = obj.transform;
        pos = objTransform.position;
        return pos;
    }

    void CarMove()
    {
        float speed_forward = 300f;
        float speed_rotate = 500f;
        if (Input.GetKey(KeyCode.W))
        {
            WheelSpeed(speed_forward, speed_forward);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            WheelSpeed(speed_rotate, -speed_rotate);
        }
        else if (Input.GetKey(KeyCode.A))
        {
            WheelSpeed(-speed_rotate, speed_rotate);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            WheelSpeed(-speed_forward, -speed_forward);
        }
        else
        {
            WheelSpeed(0f, 0f);
        }
    }

    void actionFinish()  // 動作完成
    {
        Dictionary<string, object> message1 = new Dictionary<string, object>
        {
            { "op", "publish" },
            { "id", "1" },
            { "topic", Unity2AI_action_finish_Topic },
            { "msg", new Dictionary<string, string>
                {
                    { "data", "0"}
                }
            }
        };
        string jsonMessage1 = MiniJSON.Json.Serialize(message1);
        try
        {
            socket.Send(jsonMessage1);
        }
        catch
        {
            Debug.Log("error-send");
        }
    }

    void WheelSpeed(float leftWheel, float rightWheel)
    {
        Robot.Action action = new Robot.Action();
        action.voltage = new List<float>();

        action.voltage.Add(leftWheel);
        action.voltage.Add(rightWheel);
        robot.DoAction(action);

        if (leftWheel != 0f && rightWheel != 0f)
        {
            State state = robot.GetState(newTarget);
            Send(state);
        }
    }



    private void OnWebSocketMessage(object sender, MessageEventArgs e)
    {
        string jsonString = e.Data;
        RobotNewsMessage message = JsonUtility.FromJson<RobotNewsMessage>(jsonString);
        // float[] data = message.msg.data;

        switch (message.topic)
        {
            case "/AI_2_Unity":
                HandleAI2UnityReceiveTopic(message);
                
                break;
            case "/AI_2_Unity_RESET_flag":
                HandleAI2UnityResetTopic(message);
                break;
            case "/AI2Unity_action_reset_Topic":
                float[] reset_wheel = new float[] { 4.0f};
                ExecuteRobotAction(reset_wheel, robot);
                Debug.Log("idusaiudjasijudisla");
                break;
            default:
                Debug.Log("Received message on unknown topic: " + message.topic);
                break;
        }
    }

    // float MapRange(float value, float from1, float to1, float from2, float to2)
    // {
    //     return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    // }
    // float MapData(float value)
    // {
    //     if (value >= 0)
    //     {
    //         return MapRange(value, 0, 10, 300, 400);
    //     }
    //     else
    //     {
    //         return MapRange(value, 0, -10, -300, -400);
    //     }
    // }

    public void ExecuteRobotAction(float[] data, Robot robot)
    {
        if (data == null || robot == null || data.Length < 1)
        {
            return;
        }

        float right = 0.0f;
        float left = 0.0f;
        float speed = 500.0f;

        if(data[0] == 0.0f){
            right = speed;
            left = speed;
        }
        else if(data[0] == 1.0f){
            right = -speed;
            left = speed;
        }
        else if(data[0] == 2.0f){
            right = speed;
            left = -speed;
        }
        else if(data[0] == 3.0f){
            right = -speed;
            left = -speed;
        }
        else if(data[0] == 4.0f){
            right = 0.0f;
            left = 0.0f;
        }
        
        Robot.Action action = new Robot.Action();
        action.voltage = new List<float>();
        action.voltage.Add(right);
        action.voltage.Add(left);

        // 在機器人上執行動作
        robot.DoAction(action);
        key = 1;
    }

    private void HandleAI2UnityReceiveTopic(RobotNewsMessage message)
    {   
        float[] data = message.msg.data;

        ExecuteRobotAction(data, robot);
    }

    private void HandleAI2UnityResetTopic(RobotNewsMessage message)
    {        
        target_change_flag = 1;
    }

    State updateState(Vector3 newTarget)
    {
        State state = robot.GetState(newTarget);

        return state;
    }
    private float randomFloat(float min, float max)
    {
        return (float)(random.NextDouble() * (max - min) + min);
    }

    private List<Vector3> findPeak(Vector3 start, Vector3 end)
    {
        Vector3 mid = (start + end) / 2;
        float minX = Mathf.Min(start[0], end[0]);
        float maxX = Mathf.Max(start[0], end[0]);
        float minY = Mathf.Min(start[2], end[2]);
        float maxY = Mathf.Max(start[2], end[2]);

        Vector3 c0 = new Vector3(randomFloat(minX, maxX), start[1], randomFloat(minY, minY + Mathf.Abs(maxY - minY) * 0.7f));
        Vector3 c1 = new Vector3(randomFloat(minX, maxX), start[1], randomFloat(maxY - Mathf.Abs(maxY - minY) * 0.7f, maxY));

        List<Vector3> result = new List<Vector3> { c0, c1 };
        return result;
    }
    void Send(object data) //send data to AI
    {
        var properties = typeof(State).GetProperties();
        Dictionary<string, object> stateDict = new Dictionary<string, object>();

        foreach (var property in properties)
        {
            string propertyName = property.Name;
            var value = property.GetValue(data);
            stateDict[propertyName] = value;
        }

        string dictData = MiniJSON.Json.Serialize(stateDict);

        Dictionary<string, object> message = new Dictionary<string, object>
        {
            { "op", "publish" },
            { "id", "1" },
            { "topic", Unity2AI_Topic },
            { "msg", new Dictionary<string, object>
                {
                    { "data", dictData}
                }
           }
        };

        string jsonMessage = MiniJSON.Json.Serialize(message);

        try
        {
            socket.Send(jsonMessage);
        }
        catch
        {
            Debug.Log("error-send");
        }
    }
    private void SubscribeToTopic(string topic)
    {
        string subscribeMessage = "{\"op\":\"subscribe\",\"id\":\"1\",\"topic\":\"" + topic + "\",\"type\":\"std_msgs/msg/Float32MultiArray\"}";
        socket.Send(subscribeMessage);
        
    }

    bool IsPointInsidePolygon(Vector3 point, Vector3[] polygonVertices)
    {
        int polygonSides = polygonVertices.Length;
        bool isInside = false;

        for (int i = 0, j = polygonSides - 1; i < polygonSides; j = i++)
        {
            if (((polygonVertices[i].z <= point.z && point.z < polygonVertices[j].z) ||
                (polygonVertices[j].z <= point.z && point.z < polygonVertices[i].z)) &&
                (point.x < (polygonVertices[j].x - polygonVertices[i].x) * (point.z - polygonVertices[i].z) / (polygonVertices[j].z - polygonVertices[i].z) + polygonVertices[i].x))
            {
                isInside = !isInside;
            }
        }
        return isInside;
    }

    void change_target() 
    // 新的一輪 換car、target position
    //  這邊會更新newtarget值
    {
        carPos = baselink.GetComponent<ArticulationBody>().transform.position;
        outerPolygonVertices = new Vector3[]{
            anchor1.transform.position,
            anchor2.transform.position,
            anchor3.transform.position,
            anchor4.transform.position
        };
        // -------------------------------------
         do {
        target_x_car = Random.Range(-3.0f, 3.0f);
        target_x_car = abs_biggerthan1(target_x_car);
        target_y_car = Random.Range(-3.0f, 3.0f);
        target_y_car = abs_biggerthan1(target_y_car);
        newTarget_car = new Vector3(carPos[0] + target_x_car, -1.608f, carPos[2] + target_y_car);
    } while (!IsPointInsidePolygon(newTarget_car, outerPolygonVertices));

        MoveRobot(newTarget_car);

        do {
        target_x = Random.Range(-3.0f, 3.0f);
        target_x = abs_biggerthan1(target_x);
        target_y = Random.Range(-3.0f, 3.0f);
        target_y = abs_biggerthan1(target_y);
        newTarget = new Vector3(newTarget_car[0] + target_x, -1.608f, newTarget_car[2] + target_y);
    } while (!IsPointInsidePolygon(newTarget, outerPolygonVertices) || Vector3.Distance(newTarget, newTarget_car) < 3);


        MoveGameObject(target, newTarget);

        State state = updateState(newTarget);
        Send(state);
        
    }

    void get_position_test()
    {
       Vector3 targetPosition = target.transform.position;
       State state = updateState(newTarget);
       Send(state);
    }

    private float abs_biggerthan1(float random)
    {
        if (random <= 1 && random >= -1)
        {
            if (random > 0)
            {
                random += 1;
            }
            else
            {
                random -= 1;
            }
        }
        return random;
    }
}