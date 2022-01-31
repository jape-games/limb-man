﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jape;
using Sirenix.OdinInspector;
using UnityEngine;

namespace JapeNet
{
    public class NetManager : Manager<NetManager>
    {
        private new static bool InitOnLoad => true;

        public enum Mode { Offline, Client, Server }

        private static NetSettings settings;
        public static NetSettings Settings = settings ?? (settings = Game.Settings<NetSettings>());

        internal List<NetElement> runtimeNetElements = new List<NetElement>();

        [NonSerialized] public Action OnStartClient = delegate {};
        [NonSerialized] public Action OnStopClient = delegate {};
        [NonSerialized] public Action<int> OnConnectClient = delegate {};
        [NonSerialized] public Action<int> OnPlayerConnectClient = delegate {};
        [NonSerialized] public Action<int> OnPlayerDisconnectClient = delegate {};
        [NonSerialized] public Action<int> OnDisconnectClient = delegate {};

        [NonSerialized] public Action OnStartServer = delegate {};
        [NonSerialized] public Action OnStopServer = delegate {};
        [NonSerialized] public Action OnPlayerConnectServerFirst = delegate {};
        [NonSerialized] public Action<int> OnPlayerConnectServer = delegate {};
        [NonSerialized] public Action<int> OnPlayerSceneChangeServer = delegate {};
        [NonSerialized] public Action<int> OnPlayerDisconnectServer = delegate {};
        [NonSerialized] public Action OnPlayerDisconnectServerLast = delegate {};

        public List<SyncedInstance> SyncedInstances { get; } = new List<SyncedInstance>();

        private Mode mode;

        private static SyncedInstance UpdateInstance(GameObject instance)
        {
            SyncedInstance syncedInstance = Instance.SyncedInstances.FirstOrDefault(i => i.Instance == instance);
            if (syncedInstance == null) { return SyncInstance(SyncedInstance.State.Default, instance); }
            return syncedInstance;
        }

        private static SyncedInstance SyncInstance(SyncedInstance.State state, GameObject instance, GameObject prefab = null)
        {
            SyncedInstance syncedInstance = new SyncedInstance(state, instance, prefab);
            Instance.SyncedInstances.Add(syncedInstance);
            return syncedInstance;
        }

        private static void DesyncInstance(string key) { DesyncInstance(Instance.SyncedInstances.FirstOrDefault(i => i.Key == key)); }
        private static void DesyncInstance(GameObject instance) { DesyncInstance(Instance.SyncedInstances.FirstOrDefault(i => i.Instance == instance)); }
        private static void DesyncInstance(SyncedInstance syncedInstance)
        {
            if (syncedInstance == null)
            {
                Log.Write("Cannot find synced instance");
                return;
            }
            Instance.SyncedInstances.Remove(syncedInstance);
        }

        private static void DesyncScene(int sceneIndex)
        {
            SyncedInstance[] syncedInstances = Instance.SyncedInstances.Where(i => i.SceneIndex == sceneIndex).ToArray();
            foreach (SyncedInstance instance in syncedInstances) { DesyncInstance(instance); }
        }

        public static Mode GetMode() => IsQuitting() ? Mode.Offline : Instance.mode;

        protected override void Init()
        {
            EngineManager.AddShell(typeof(NetShell));
            InitDelegates();
            JapeNet.Client.Client.Init();
            JapeNet.Server.Server.Init();
            this.Log().ToggleDiagnostics();
        }

        private static void InitDelegates()
        {
            if (!EngineManager.Instance.TryGetComponent(out NetShell shell)) { return; }
            foreach (KeyValuePair<string, Action<object[]>> @delegate in shell.Delegates)
            {
                JapeNet.Server.Server.NetDelegator.Add(@delegate.Key, @delegate.Value);
            }
        }

        public static void Connect(int timeout, Action success, Action error)
        {
            Instance.mode = Settings.IsServer() ? 
                            Mode.Server :
                            Mode.Client;

            switch (Instance.mode)
            {
                case Mode.Client:
                    JapeNet.Client.Client.Start(success, error, timeout);
                    break;

                case Mode.Server:
                    JapeNet.Server.Server.Start(success, error);
                    break;
            }
        }

        public static void Connect(Action success, Action error) { Connect(0, success, error); }
        public static void Connect(int timeout = 0) { Connect(timeout, null, null); }

        public static void Disconnect()
        {
            switch (Instance.mode)
            {
                case Mode.Client:
                    JapeNet.Client.Client.Stop();
                    break;

                case Mode.Server:
                    JapeNet.Server.Server.Stop();
                    break;
            }

            Instance.mode = Mode.Offline;
        }

        protected override void PreQuit()
        {
            Disconnect();
        }

        internal void WebSocketSend(byte[] data)
        {
            WebManager.Socket.Send(data);
        }

        internal void WebSocketReceive(byte[] data)
        {
            JapeNet.Client.Client.server.web.Receive(data);
        }

        internal static void StartClient()
        {
            EngineManager.AddShell(typeof(ClientShell));
            Instance.OnStartClient.Invoke();
        }

        internal static void StopClient()
        {
            Instance.OnStopClient.Invoke();
            EngineManager.RemoveShell(typeof(ClientShell));
        }

        internal static void ConnectClient(int id)
        {
            Instance.OnConnectClient.Invoke(id);
        }

        internal static void DisconnectClient(int id)
        {
            Instance.OnDisconnectClient.Invoke(id);
        }

        internal static void PlayerConnectClient(int id)
        {
            Instance.OnPlayerConnectClient.Invoke(id);
        }

        internal static void PlayerDisconnectClient(int id)
        {
            Instance.OnPlayerDisconnectClient.Invoke(id);
        }

        internal static void StartServer()
        {
            EngineManager.AddShell(typeof(ServerShell));
            if (Settings.IsServerBuild()) { Master.ServerActivate(); }
            Instance.OnStartServer.Invoke();
        }

        internal static void StopServer()
        {
            Instance.OnStopServer.Invoke();
            EngineManager.RemoveShell(typeof(ServerShell));
        }

        internal static void PlayerConnectServer(int id)
        {
            if (Settings.IsServerBuild()) { Master.PlayerConnect(); } 
            if (JapeNet.Server.Server.GetConnectedClients().Length == 1) { Instance.OnPlayerConnectServerFirst.Invoke(); }
            Instance.OnPlayerConnectServer.Invoke(id);
        }

        internal static void PlayerSceneChangeServer(int id)
        {
            Net.Server.RestoreClient(id);
            Server.Sync(id);
            Instance.OnPlayerSceneChangeServer.Invoke(id);
        }

        internal static void PlayerDisconnectServer(int id)
        {
            Instance.OnPlayerDisconnectServer.Invoke(id);
            if (JapeNet.Server.Server.GetConnectedClients().Length == 0) { Instance.OnPlayerDisconnectServerLast.Invoke(); }
            if (Settings.IsServerBuild()) { Master.PlayerDisconnect(); } 
        }

        internal static GameObject Spawn(string id, string prefabName, Vector3 position, Quaternion rotation, string parentId, int player, bool active, bool temporary)
        {
            GameObject prefab = Database.LoadPrefab(prefabName);

            if (prefab == null) { Log.Write("Could not find prefab to spawn"); return null; }

            GameObject clone = Game.CloneGameObjectInactive(prefab, position, rotation, TryGetParent(parentId));

            clone.Properties().Id = id;
            clone.SetPlayer(player);
            clone.SetActive(active);

            if (!temporary) { SyncInstance(SyncedInstance.State.Spawned, clone, prefab); }

            return clone;
        }

        internal static void Despawn(string id)
        {
            GameObject instance =  Game.Find<GameObject>().FirstOrDefault(g => g.Identifier() == id);

            if (instance == null) { Log.Write("Could not find instance GameObject"); return; }

            SyncedInstance syncedInstance = Instance.SyncedInstances.FirstOrDefault(i => i.Instance == instance);

            if (syncedInstance == null) { SyncInstance(SyncedInstance.State.Despawned, instance); return; }

            switch (syncedInstance.GetState())
            {
                case SyncedInstance.State.Spawned:
                    DesyncInstance(instance);
                    break;

                case SyncedInstance.State.Despawned:
                    Log.Write("Already despawned");
                    return;

                default:
                    syncedInstance.SetState(SyncedInstance.State.Despawned);
                    break;
            }

            Destroy(instance);
        }

        internal static void Parent(string id, string parentId)
        {
            GameObject instance = Game.Find<GameObject>().FirstOrDefault(g => g.Identifier() == id);

            if (instance == null) { Log.Write("Could not find instance GameObject"); return; }

            GameObject parent = Game.Find<GameObject>().FirstOrDefault(g => g.Identifier() == parentId);

            if (instance == null) { Log.Write("Could not find parent GameObject"); return; }

            instance.transform.SetParent(parent.transform);

            UpdateInstance(instance);
        }

        internal static void SetActive(string id, bool value)
        {
            GameObject instance = Game.Find<GameObject>().FirstOrDefault(g => g.Identifier() == id);

            if (instance == null) { Log.Write("Could not find instance GameObject"); return; }

            instance.SetActive(value);

            UpdateInstance(instance);
        }

        internal static void SceneChange(string scenePath, Action onLoad)
        {
            if (Game.ActiveScene().path == scenePath) { Log.Write("Scene already active"); onLoad?.Invoke(); return; }
            DesyncScene(Game.ActiveScene().buildIndex);
            EngineManager.ChangeScene(scenePath, null, onLoad);
        }

        internal static string GetParentId(Transform parent)
        {
            return parent == null ? string.Empty : parent.gameObject.Identifier(); 
        }

        internal static Transform TryGetParent(string id)
        {
            if (string.IsNullOrEmpty(id)) { return null; }
            GameObject parent = Game.Find<GameObject>().FirstOrDefault(g => g.Identifier() == id);
            if (parent == null)
            {
                Log.Write($"Could not find parent: {id}");
                return null;
            }
            return parent.transform;
        }

        internal static class Client
        {
            internal static void AccessElement(byte[] elementKey, Action<NetElement> action)
            {
                NetElement target = null;
                foreach (NetElement element in Instance.runtimeNetElements)
                {
                    if (!element.Key.Compare(elementKey)) { continue; }
                    target = element;
                }
                if (target == null) { Instance.Log().Diagnostic($"Could not find element key: {elementKey}"); return; }
                action.Invoke(target);
            }
        }

        internal static class Server
        {
            internal static void AccessElement(int client, byte[] elementKey, Action<NetElement> action)
            {
                NetElement target = null;
                foreach (NetElement element in Instance.runtimeNetElements)
                {
                    if (!element.Key.Compare(elementKey)) { continue; }
                    target = element;
                    break;
                }
                if (target == null) { Instance.Log().Diagnostic($"Could not find element key: {elementKey}"); return; }
                if (!target.CanAccess(client)) { Log.Write($"Player {client}: Can not access {target}"); return; }
                action.Invoke(target);
            }

            internal static void Sync(int client)
            {
                foreach (SyncedInstance syncedInstance in Instance.SyncedInstances)
                {
                    switch (syncedInstance.GetState())
                    {
                        case SyncedInstance.State.Spawned:
                            Net.Server.SpawnLocal
                            (
                                client,
                                syncedInstance.Key,
                                syncedInstance.Prefab,
                                syncedInstance.Instance.transform.position,
                                syncedInstance.Instance.transform.rotation,
                                syncedInstance.Instance.transform.parent,
                                syncedInstance.Instance.gameObject.Player(),
                                syncedInstance.Instance.activeSelf
                            );
                            break;

                        case SyncedInstance.State.Despawned:
                            Net.Server.DespawnLocal(client, syncedInstance.Key);
                            break;

                        default:
                            Net.Server.SetActive(syncedInstance.Instance, syncedInstance.Instance.activeSelf);
                            break;
                    }
                }
            }
        }
    }
}