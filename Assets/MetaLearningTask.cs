using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using UnityEngine.PlayerLoop;
using System.Collections;
using Unity.MLAgents.Policies;
using Unity.Barracuda;
using System.Data;

#if UNITY_EDITOR
using UnityEditor;

#endif 
using UnityEngine.Assertions;



public class MetaLearningTask : Agent
{
    [HideInInspector]
    public bool runEnable = true;

    List<GameObject> datasetList = new List<GameObject>();
    List<GameObject> selectedObjs = new List<GameObject>();
    List<GameObject> cloneObjs = new List<GameObject>();
  
    [HideInInspector]
    public GameObject cameraContainer;
    //Rigidbody rBody;
    [HideInInspector]
    public GameObject agent;
    Dictionary<string, int> mapNameToNum = new Dictionary<string, int>();

    List<Vector3> positions = new List<Vector3>();
    List<int> labelsSupport = new List<int>();
    List<int> labelsTesting = new List<int>();

    int N;
    int K;
    int Q;
    int sizeCanvas;
    string nameDataset;

    List<GameObject> supportCameras = new List<GameObject>();
    List<GameObject> testingCameras = new List<GameObject>();



    public void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }


    void Start()
    {
        GameObject cameraContainer = GameObject.Find("CameraContainer");
        GameObject info = GameObject.Find("Info");
        string infostr = info.transform.GetChild(0).name;
        var tmp = infostr.Split('_');
        N = int.Parse(tmp[0].Split(':')[1]);
        K = int.Parse(tmp[1].Split(':')[1]);
        Q = int.Parse(tmp[2].Split(':')[1]);
        sizeCanvas = int.Parse(tmp[3].Split(':')[1]);
        nameDataset = tmp[4].Split(':')[1];

        // Rebuild the list

        int totIndex = 0;
        for (int k = 0; k < K; k++)
        {
            for (int n = 0; n < N; n++)
            {
                supportCameras.Add(cameraContainer.transform.Find(totIndex.ToString("D3") + "_SupportCamera").gameObject);
                totIndex += 1;
            }
        }

        for (int k = 0; k < K; k++)
        {
            for (int q = 0; q < Q; q++)
            {
                testingCameras.Add(cameraContainer.transform.Find(totIndex.ToString("D3") + "_QueryCamera").gameObject);
                totIndex += 1;
            }
        }

        //var b  =prova.previousK;
        // Collect all the objects in the Dataset game object
        GameObject dataset;
       
       dataset = GameObject.Find(nameDataset);
        
        int children = dataset.transform.childCount;
        for (int i = 0; i < children; i++)
        {
            datasetList.Add(dataset.transform.GetChild(i).gameObject);
            mapNameToNum.Add(datasetList[i].name, i);
        }
        Assert.IsTrue(datasetList.Count >= K, "The elements in the datasetList are less than K!");

        // Create Positions
        for (int k = 0; k < K; k++)
        {
            positions.Add(new Vector3(5 * k, 0, 0));
        }



    }

    //public Transform Target;
    public CameraSensor VisualObs;
    public override void OnEpisodeBegin()
    {
        selectedObjs.Clear();
        cloneObjs.Clear();
      
        GameObject episodeContainer = new GameObject("Episode Container");
        // Draw K classes from the list
        List<int> listNumbers = new List<int>();
        int number;
        for (int k = 0; k < K; k++)
        {
            do
            {
                number = Random.Range(0, datasetList.Count);
            } while (listNumbers.Contains(number));
            listNumbers.Add(number);
            selectedObjs.Add(datasetList[number]);
        };

        // Place the selected objects on their position
        int stop = 1;
        for (int k = 0; k < K; k++)
        {
            cloneObjs.Add(Instantiate(selectedObjs[k], positions[k], Quaternion.identity));
            cloneObjs[k].transform.parent = episodeContainer.transform;
            cloneObjs[k].name = selectedObjs[k].name;
            // Unity generates 32 layers. Layers from 8 and above are unused.
            SetLayerRecursively(cloneObjs[k], 8 + k);
        }

        // Place the query Cameras somewhere facing the object, looking at it
        float distance = 4;
        int queryIndex = 0;
        for (int k = 0; k < K; k++)
        {
            Vector3 direction = Vector3.Normalize(new Vector3(Random.Range(-1.0F, 1.0F), Random.Range(-1.0F, 1.0F), Random.Range(-1.0F, 1.0F)));
            Vector3 position = cloneObjs[k].transform.position + distance * direction;
            for (int q = 0; q < Q; q++)
            {
                testingCameras[queryIndex].transform.position = new Vector3(
                        position.x + Random.Range(-0.5F, 0.5F),
                        position.y + Random.Range(-0.5F, 0.5F),
                        position.z);
                testingCameras[queryIndex].transform.LookAt(cloneObjs[k].transform);
                testingCameras[queryIndex].GetComponent<Camera>().cullingMask = 1 << (8 + k);
                labelsTesting.Add(mapNameToNum[cloneObjs[k].name]);
                queryIndex += 1;
            }

            // Move the support cameras
            for (int n = 0; n < N; n++)
            {
                // apply random translation on the x and y 
                    supportCameras[n + N * k].transform.position = new Vector3(
                        position.x + Random.Range(-0.5F, 0.5F),
                        position.y + Random.Range(-0.5F, 0.5F),
                        position.z);
                supportCameras[n + N * k].transform.LookAt(cloneObjs[k].transform);
                supportCameras[n + N * k].GetComponent<Camera>().cullingMask = 1 << (8 + k);
                labelsSupport.Add(mapNameToNum[cloneObjs[k].name]);

            }
        }
        //UnityEngine.Debug.Log("WAITING!");
        //StartCoroutine(waiter());
        this.RequestDecision();
    }
    IEnumerator waiter()
    {

        //Wait for 4 seconds
        yield return new WaitForSeconds(4);
    }
  
    public override void Heuristic(float[] actionsOut)
    {
        if (Input.GetKeyDown("space"))
        {
            UnityEngine.Debug.Log("SPACE");
            foreach (GameObject cln in cloneObjs)
            {
                Destroy(cln);
            }
            labelsSupport.Clear();
            labelsTesting.Clear();
            GameObject episodeContainer = GameObject.Find("Episode Container");
            Destroy(episodeContainer);
            OnEpisodeBegin();

        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        var envParameters = Academy.Instance.EnvironmentParameters;

        List<int> labelsAll = new List<int>();

        // Another way of doing that is to use channels, but for now this is fine.

        for (int i = 0; i <  labelsSupport.Count ;  i++)
        {
            sensor.AddObservation(labelsSupport[i]);
        }

        for (int i = 0; i <  labelsTesting.Count; i++)
        {
            sensor.AddObservation(labelsTesting[i]);
        }
    }


    public override void OnActionReceived(float[] vectorAction)
    {
        //// Actions, size = 2 
        //Vector3 controlSignal = Vector3.zero;
        //controlSignal.x = vectorAction[0];
        //controlSignal.z = vectorAction[1];
        //rBody.AddForce(controlSignal * forceMultiplier);

        //// Rewards
        //float distanceToTarget = Vector3.Distance(this.transform.localPosition, Target.localPosition);

        //// Reached target
        //if (distanceToTarget < 1.42f)
        //{
        //    SetReward(1.0f);
        //    EndEpisode();
        //}

        //// Fell off platform
        //if (this.transform.localPosition.y < 0)
        //{
        //EndEpisode();
        //}
        foreach (GameObject cln in cloneObjs)
        {
            Destroy(cln);
        }
        labelsSupport.Clear();
        labelsTesting.Clear();
        GameObject episodeContainer = GameObject.Find("Episode Container");
        Destroy(episodeContainer);
        OnEpisodeBegin();
    }

}

#if UNITY_EDITOR

//[System.Serializable]
[ExecuteInEditMode]
[CustomEditor(typeof(MetaLearningTask))]
//[CanEditMultipleObjects]
public class MLtaskEditor : Editor
{

    int N;
    int K;
    int Q;
    int sizeCanvas;
    string nameDataset;
    public Object source;
    void OnEnable()
    {
        var mt = (MetaLearningTask)target;
        if (mt.runEnable)
        {
            mt.agent = GameObject.Find("Agent");
            UnityEngine.Debug.Log("HI");
            //BuildSceneCLI.UpdateComponents(mt.N, mt.K, mt.Q, mt.sizeCanvas);
            mt.runEnable = false;           
        }

    }



    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying)
        {
            base.OnInspectorGUI();
            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            //serializedObject.Update();
            var mt = (MetaLearningTask)target;
            GameObject info = GameObject.Find("Info");
            string infostr = info.transform.GetChild(0).name;
            var tmp = infostr.Split('_');
            N = int.Parse(tmp[0].Split(':')[1]);
            K = int.Parse(tmp[1].Split(':')[1]);
            Q = int.Parse(tmp[2].Split(':')[1]);
            sizeCanvas = int.Parse(tmp[3].Split(':')[1]);
            source = GameObject.Find(tmp[4].Split(':')[1]);



            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Size Canvas"), GUILayout.Width(80));
            int currentSC = EditorGUILayout.IntSlider(sizeCanvas, 10, 250);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("N"), GUILayout.Width(20));
            int currentN = EditorGUILayout.IntSlider(N, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("K"), GUILayout.Width(20));
            int currentK = EditorGUILayout.IntSlider(K, 1, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Q"), GUILayout.Width(20));
            int currentQ = EditorGUILayout.IntSlider(Q, 1, 30);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Dataset Obj."), GUILayout.Width(70));
            source = EditorGUILayout.ObjectField(source, typeof(Object), true);
            EditorGUILayout.EndHorizontal();

            string currentDS = null;
            if (source != null)
            {
                currentDS = source.name;
                //UnityEngine.Debug.Log(currentDS);
            }
            if (GUI.changed)
            {
                if (currentN != N || currentQ != Q || currentK != K || currentSC != sizeCanvas || currentDS != nameDataset)
                {
                    BuildSceneCLI.N = currentN;
                    BuildSceneCLI.K = currentK;
                    BuildSceneCLI.Q = currentQ;
                    BuildSceneCLI.sizeCanvas = currentSC;
                    if (currentDS != null)
                    {
                        BuildSceneCLI.nameDataset = currentDS;
                    }
                    BuildSceneCLI.UpdateComponents();

                }

            }
        }
    }
}
#endif
