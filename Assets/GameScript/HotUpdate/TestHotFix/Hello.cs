using System.Collections;
using UnityEngine;

public class Hello
{
    public static void Run()
    {
        Debug.Log("Hello, 嘎嘎");
        GameObject go = new GameObject("Test1");
        go.AddComponent<Print>();

        GameObject go2 = new GameObject("TestConsole2Screen");
        go2.AddComponent<ConsoleToScreen>();

        GameObject go3 = new GameObject("TestGameCore");
        go3.AddComponent<GameCore>();


        GameObject go4 = new GameObject("TestGameCore");
        go4.AddComponent<GameCore>();
    }
}