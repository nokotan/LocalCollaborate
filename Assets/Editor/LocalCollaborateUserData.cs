using UnityEngine;
using UnityEditor;

public class LocalCollaborateUserData : ScriptableObject {
    [SerializeField]
    string userName;
    [SerializeField]
    string eMailAddress;

    static LocalCollaborateUserData Instance
    {
        get
        {
            var Instance = AssetDatabase.LoadAssetAtPath<LocalCollaborateUserData>("Assets/Editor/LocalCollaborateUserData.asset");

            if (Instance == null)
            {
                Instance = CreateInstance<LocalCollaborateUserData>();
                AssetDatabase.CreateAsset(Instance, "Assets/Editor/LocalCollaborateUserData.asset");
            }

            return Instance;
        }
    }

    public static string UserName
    {
        get
        {
            return Instance.userName;
        }
        set
        {
            Undo.RecordObject(Instance, "LocalCollaborate");
            Instance.userName = value;
        }
    }

    public static string EMailAddress
    {
        get
        {
            return Instance.eMailAddress;
        }
        set
        {
            Undo.RecordObject(Instance, "LocalCollaborate");
            Instance.eMailAddress = value;
        }
    }

    public static LibGit2Sharp.Signature GetSignature()
    {
        return new LibGit2Sharp.Signature(UserName, EMailAddress, System.DateTime.Now);
    }
}
