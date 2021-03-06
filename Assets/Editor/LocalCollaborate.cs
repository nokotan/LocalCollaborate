﻿using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using LibGit2Sharp;

public class LocalCollaborate : EditorWindow {

    class SettingTab
    {
        public string RemotePath { get; set; }
        public string UserName { get; set; }
        public string EMailAddress { get; set; }
        public Action<string, string> OnUserDataChanged { get; set; }

        void OnGUIInternal(string name, Func<string> updateFunction)
        {
            EditorGUILayout.LabelField(name);

            EditorGUI.BeginChangeCheck();
            var userData = updateFunction();

            if (EditorGUI.EndChangeCheck())
            {
                OnUserDataChanged.Invoke(name, userData);
            }
        }

        public void OnGUI()
        {
            OnGUIInternal("RemotePath",     () => RemotePath = EditorGUILayout.TextField(RemotePath));
            OnGUIInternal("UserName",       () => UserName = EditorGUILayout.TextField(UserName));
            OnGUIInternal("EMailAddress",   () => EMailAddress = EditorGUILayout.TextField(EMailAddress));
        }
    }

#region CommitList

    struct CommitBaseData
    {
        public string Id { get; set; }
        public string Auther { get; set; }
        public string Message { get; set; }
    }

    List<CommitBaseData> CommitData;

    void UpdateCommitData()
    {
        var masterBranch = LocalRepository.Branches["master"];

        CommitData = masterBranch.Commits.Select(
            commit => new CommitBaseData()
            {
                Id = commit.Id.ToString().Substring(0, 8),
                Auther = commit.Author.Name,
                Message = commit.Message
            })
            .ToList();

        Debug.Log("Commit Data Updated");
    }

#endregion

#region FileList

    struct FileStatusBaseData
    {
        public FileStatus status { get; set; }
        public string filePath { get; set; }
    }

    List<FileStatusBaseData> FileStatusData;

    void UpdateFileStatus()
    {
        FileStatusData = LocalRepository.RetrieveStatus()
            .Where(item => item.State != FileStatus.Ignored)
            .Select(
                item => new FileStatusBaseData()
                {
                    status = item.State,
                    filePath = item.FilePath
                })
            .ToList();

        Debug.Log("File Status Updated");
    }

    #endregion

#region Diff

    List<FileStatusBaseData> DiffFileStatusData = new List<FileStatusBaseData>();
    List<FileStatusBaseData> DiffFileStatusDataRemote = new List<FileStatusBaseData>();

    void UpdateRemoteChangesList()
    {
        var MergeBase = LocalRepository.ObjectDatabase.FindMergeBase(LocalRepository.Head.Tip, LocalRepository.Head.TrackedBranch.Tip);

        DiffFileStatusData.Clear();

        var Diff = LocalRepository.Diff.Compare<TreeChanges>(MergeBase.Tree, LocalRepository.Head.Tip.Tree);

        foreach (var item in Diff.Modified)
        {
            DiffFileStatusData.Add(new FileStatusBaseData() { filePath = item.Path, status = FileStatus.ModifiedInWorkdir });
        }

        DiffFileStatusDataRemote.Clear();

        Diff = LocalRepository.Diff.Compare<TreeChanges>(MergeBase.Tree, LocalRepository.Head.TrackedBranch.Tip.Tree);

        foreach (var item in Diff.Modified)
        {
            DiffFileStatusDataRemote.Add(new FileStatusBaseData() { filePath = item.Path, status = FileStatus.ModifiedInWorkdir });
        }

        Debug.Log("Updated File Status");
    }

    Repository LocalRepository;
    SettingTab settingTab;

    #endregion

#region Setup

    [MenuItem("Window/LocalCollaborate")]
    static void Open()
    {
        GetWindow<LocalCollaborate>();
    }

    void InitializeSettingTab()
    {
        settingTab = new SettingTab()
        {
            UserName = LocalCollaborateUserData.UserName,
            EMailAddress = LocalCollaborateUserData.EMailAddress
        };
        
        // RemotePath Changed Handler
        settingTab.OnUserDataChanged += (dataName, url) =>
        {
            if (dataName == "RemotePath")
            {
                var remoteList = LocalRepository.Network.Remotes;
                var remote = remoteList["origin"];

                if (remote == null)
                {
                    remote = remoteList.Add("origin", url);
                }
                else
                {
                    remoteList.Update(remote, r => r.Url = url);
                }

                var masterBranch = LocalRepository.Branches["master"];

                LocalRepository.Branches.Update(masterBranch,
                    b => b.Remote = remote.Name,
                    b => b.UpstreamBranch = masterBranch.CanonicalName
                );
            }
        };

        // UserName Changed Handler
        settingTab.OnUserDataChanged += (dataName, userName) =>
        {
            if (dataName == "UserName")
            {
                LocalCollaborateUserData.UserName = userName;
            }
        };

        // EMailAddress Changed Handler
        settingTab.OnUserDataChanged += (dataName, eMailAddress) =>
        {
            if (dataName == "EMailAddress")
            {
                LocalCollaborateUserData.EMailAddress = eMailAddress;
            }
        };
    }

    bool InitializeRepository()
    {
        if (Repository.IsValid("."))
        {
            LocalRepository = new Repository(".");

            var remote = LocalRepository.Network.Remotes["origin"];

            if (remote != null)
            {
                settingTab.RemotePath = remote.Url;
            }

            UpdateCommitData();
            UpdateFileStatus();
            UpdateRemoteChangesList();
            
            return false;
        }
        else
        {
            return true;
        }
    }

#endregion

    private void OnEnable()
    {
        Debug.Log("OnEnable");
        
        InitializeSettingTab();
        InitializeRepository();

        LastFetchedTime = DateTime.Now;
        CommitData = new List<CommitBaseData>();
    }

    bool HasRemoteChange
    {
        get
        {
            if (LocalRepository == null || LocalRepository.Head.TrackedBranch == null)
            {
                return false;
            }

            var trackingBranch = LocalRepository.Head.TrackedBranch;
            var log = LocalRepository.Commits.QueryBy(new CommitFilter
            { IncludeReachableFrom = trackingBranch.Tip.Id, ExcludeReachableFrom = LocalRepository.Head.Tip.Id });

            return log.Count() > 0;
        }
    }

#region Git Instructions

    void Push()
    {
        if (HasRemoteChange)
        {
            StatusString = "There is remote change! Please merge at first.";
            Debug.Log("Push Failed: Remote Change");
        }
        else
        {
            LocalRepository.Network.Push(LocalRepository.Network.Remotes["origin"], LocalRepository.Head.CanonicalName);
            StatusString = "Push Finished";

            UpdateRemoteChangesList();
            Debug.Log("Pushed");
        }       
    }

    void Fetch()
    {
        var remote = LocalRepository.Network.Remotes["origin"];
        LocalRepository.Network.Fetch(remote);

        UpdateRemoteChangesList();
        Debug.Log("Fetched");
    }

    void Merge()
    {
        var Sign = LocalCollaborateUserData.GetSignature();
        var Result = LocalRepository.Merge(LocalRepository.Head.TrackedBranch, Sign);

        if (Result.Status == MergeStatus.Conflicts)
        {
            Debug.Log("Conflict!");
        }

        AssetDatabase.Refresh();
        StatusString = "Merge Finished";

        UpdateCommitData();
        UpdateRemoteChangesList();
    }

#endregion

    DateTime LastFetchedTime;

    void OnInspectorUpdate()
    {
        if (DateTime.Now - LastFetchedTime > TimeSpan.FromSeconds(2.0))
        {
            Fetch();

            if (HasRemoteChange)
            {
                StatusString = "There is remote changes!";
            }

            LastFetchedTime = DateTime.Now;
        }
    }

    void OnProjectChange()
    {
        UpdateFileStatus();
    }

#region OnGUI

    int Selected;

    Vector2 DiffScrollRect;
    Vector2 ScrollRect;
    Vector2 ScrollRectCommit;
    string CommitMessage;

    string StatusString = "";

    void OnGUIInSynclonizeTab()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Diffs");

        if (GUILayout.Button("Refresh"))
        {
            UpdateRemoteChangesList();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Local Changes");

        if (GUILayout.Button("Push"))
        {
            Push();
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.IndentLevelScope())
        {
            DiffScrollRect = EditorGUILayout.BeginScrollView(DiffScrollRect, GUI.skin.box, GUILayout.MinHeight(50.0f), GUILayout.MaxHeight(500.0f));

            foreach (var item in DiffFileStatusData) // .Where(obj => obj.State != FileStatus.NewInWorkdir))
            {
                EditorGUILayout.LabelField(item.status.ToString(), item.filePath);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Remote Changes");

        if (GUILayout.Button("Merge"))
        {
            Merge();
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.IndentLevelScope())
        {
            DiffScrollRect = EditorGUILayout.BeginScrollView(DiffScrollRect, GUI.skin.box, GUILayout.MinHeight(50.0f), GUILayout.MaxHeight(500.0f));

            foreach (var item in DiffFileStatusDataRemote) // .Where(obj => obj.State != FileStatus.NewInWorkdir))
            {
                EditorGUILayout.LabelField(item.status.ToString(), item.filePath);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    void OnGUIInCommits()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Commits");

        if (GUILayout.Button("Refresh"))
        {
            UpdateCommitData();
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.IndentLevelScope())
        {
            ScrollRectCommit = EditorGUILayout.BeginScrollView(ScrollRectCommit, GUI.skin.box, GUILayout.MinHeight(50.0f), GUILayout.MaxHeight(500.0f));

            foreach (var commit in CommitData)
            {
                EditorGUILayout.LabelField(commit.Id, commit.Message);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Status");

        if (GUILayout.Button("Refresh"))
        {
            UpdateFileStatus();
        }

        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.IndentLevelScope())
        {
            ScrollRect = EditorGUILayout.BeginScrollView(ScrollRect, GUI.skin.box, GUILayout.MinHeight(50.0f), GUILayout.MaxHeight(500.0f));

            foreach (var item in FileStatusData) // .Where(obj => obj.State != FileStatus.NewInWorkdir))
            {
                EditorGUILayout.LabelField(item.status.ToString(), item.filePath);
            }

            EditorGUILayout.EndScrollView();
        }

        CommitMessage = EditorGUILayout.TextArea(CommitMessage);

        if (GUILayout.Button("Commit"))
        {
            var Sign = LocalCollaborateUserData.GetSignature();
            LocalRepository.Stage("*");
            LocalRepository.Commit(CommitMessage, Sign, Sign);

            CommitMessage = "";
            StatusString = "Commit Finished!";

            UpdateCommitData();
        }
    }

    void OnGUI()
    {
        if (LocalRepository == null && InitializeRepository())
        {
            EditorGUILayout.LabelField("Local Collaborate is not active on this project.");

            if (GUILayout.Button("Start Local Collaborate"))
            {
                Repository.Init(".");
            }

            return;
        }

        Selected = GUILayout.Toolbar(Selected, new string[] { "Commits", "Synclonize", "Setting" });

        if (Selected == 2)
        {
            settingTab.OnGUI();
        }

        if (Selected == 1)
        {
            OnGUIInSynclonizeTab();
        }

        if (Selected == 0)
        {
            OnGUIInCommits();
        }

        EditorGUILayout.LabelField(StatusString);
    }

#endregion

}
