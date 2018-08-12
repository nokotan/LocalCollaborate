﻿using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using LibGit2Sharp;

public class LocalCollaborate : EditorWindow {

    [SerializeField]
    string RemotePath;
    Repository LocalRepository;

    [MenuItem("Window/LocalCollaborate/Open")]
    static void Open()
    {
        GetWindow<LocalCollaborate>();
    }

    [SerializeField]
    int Selected;
    Vector2 ScrollRect;
    Vector2 ScrollRectCommit;
    string CommitMessage;

    [SerializeField]
    string UserName;
    [SerializeField]
    string EMail;

    string StatusString = "";

    void OnGUI()
    {
        if (LocalRepository == null)
        {
            try
            {
                LocalRepository = new Repository(".");
                RemotePath = LocalRepository.Network.Remotes["origin"].Url;
            }
            catch (RepositoryNotFoundException e)
            {
                EditorGUILayout.LabelField("This project is not git repository.");
                Debug.Log(e.Message);
                return;
            }
        }

        Selected = GUILayout.Toolbar(Selected, new string[] { "Setting", "Branches", "Commits" });

        if (Selected == 0)
        {
            EditorGUILayout.LabelField("Remote Path");

            EditorGUI.BeginChangeCheck();
            RemotePath = EditorGUILayout.TextField(RemotePath);

            Remote remote = LocalRepository.Network.Remotes["origin"];

            if (EditorGUI.EndChangeCheck())
            {

                if (remote == null)
                {
                    remote = LocalRepository.Network.Remotes.Add("origin", RemotePath);
                }
                else
                {
                    LocalRepository.Network.Remotes.Update(remote, r => r.Url = RemotePath);
                }

                LocalRepository.Branches.Update(LocalRepository.Head,
                    b => b.Remote = remote.Name,
                    b => b.UpstreamBranch = LocalRepository.Head.CanonicalName);
            }

            if (GUILayout.Button("Push"))
            {
                LocalRepository.Network.Push(LocalRepository.Head);
                StatusString = "Push Finished";
            }

            if (GUILayout.Button("Fetch"))
            {
                LocalRepository.Network.Fetch(remote);
            }

            if (GUILayout.Button("Merge"))
            {
                var Sign = new Signature(new Identity(UserName, EMail), System.DateTime.Now);
                var Result = LocalRepository.Merge(LocalRepository.Head.TrackedBranch, Sign);

                if (Result.Status == MergeStatus.Conflicts)
                {
                    Debug.Log("Conflict!");
                }

                AssetDatabase.Refresh();
                StatusString = "Merge Finished";
            }

            EditorGUILayout.LabelField("Name");
            UserName = EditorGUILayout.TextField(UserName);

            EditorGUILayout.LabelField("EMail");
            EMail = EditorGUILayout.TextField(EMail);

            var trackingBranch = LocalRepository.Head.TrackedBranch;
            var log = LocalRepository.Commits.QueryBy(new CommitFilter
            { IncludeReachableFrom = trackingBranch.Tip.Id, ExcludeReachableFrom = LocalRepository.Head.Tip.Id });

            if (log.Count() > 0)
            {
                StatusString = "There is remote changes!";
            }
        }

        if (Selected == 1)
        {
            EditorGUILayout.LabelField("Branches");

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var branch in LocalRepository.Branches)
                {
                    EditorGUILayout.LabelField(branch.FriendlyName);
                }
            }
        }

        if (Selected == 2)
        {
            EditorGUILayout.LabelField("Commits");

            using (new EditorGUI.IndentLevelScope())
            {
                ScrollRectCommit = EditorGUILayout.BeginScrollView(ScrollRectCommit);

                foreach (var commit in LocalRepository.Head.Commits)
                {
                    EditorGUILayout.LabelField(commit.Id.ToString().Substring(0, 8), commit.Message);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.LabelField("Status");

            using (new EditorGUI.IndentLevelScope())
            {
                ScrollRect = EditorGUILayout.BeginScrollView(ScrollRect);

                foreach (var item in LocalRepository.RetrieveStatus()) // .Where(obj => obj.State != FileStatus.NewInWorkdir))
                {
                    EditorGUILayout.LabelField(item.State.ToString(), item.FilePath);
                }

                EditorGUILayout.EndScrollView();
            }

            CommitMessage = EditorGUILayout.TextArea(CommitMessage);

            if (GUILayout.Button("Commit"))
            {
                var Sign = new Signature(new Identity(UserName, EMail), System.DateTime.Now);
                LocalRepository.Stage("*");
                LocalRepository.Commit(CommitMessage, Sign, Sign);

                CommitMessage = "";
                StatusString = "Commit Finished!";
            }
        }


        EditorGUILayout.LabelField(StatusString);
    }
}
