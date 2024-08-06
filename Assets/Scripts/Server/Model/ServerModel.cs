using Network.DB;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = System.Random;

namespace Network
{
    [Serializable]
    public class ServerModel
    {
        [Tooltip("データ管理、保持を行うクラス")]
        [SerializeField]
        private DataBaseManager[] _dataBaseManagers = default;

        /// <summary> 現在のクライアントの接続数 </summary>
        private int _connectCount = 0;

        /// <summary> 最大同時接続数 </summary>
        private int _maxConcurrentConnectCount = 5;
        private float _serverCloseTime = 1f;

        private Random _random = default;
        private List<string> _idList = default;

        private const int UserIDLength = 8;
        /// <summary> UserIDに使用される文字 </summary>
        private const string CharLine = "ABCDEFJHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        /// <summary> リクエストに対する一連の処理が正常に流れた時に返す文字列 </summary>
        private const string Success = "Request Success";
        /// <summary> リクエストに対する一連の処理が失敗した時に返す文字列 </summary>
        private const string Failed = "Request Failed";

        protected int ConnectCount
        {
            get => _connectCount;
            private set
            {
                _connectCount = value;
                if (_connectCount <= 0) { Task.Run(async () => await ServerCloseCounter()); }
            }
        }

        public async void Initialize(float serverCloseTime, int maxConnectCount)
        {
            _serverCloseTime = serverCloseTime;
            _maxConcurrentConnectCount = maxConnectCount;

            foreach (var db in _dataBaseManagers) { await db.Initialize(); }

            _idList ??= new();
            var sampleTable = _dataBaseManagers[0].GetDataTable();
            for (int i = 1; i < sampleTable.Count; i++) { _idList.Add(sampleTable[i][0]); }
        }

        private async Task ServerCloseCounter()
        {
            await Task.Run(async () =>
            {
                await Task.Delay((int)(_serverCloseTime * 1000));
#if UNITY_EDITOR
                if (!EditorApplication.isPlaying) { return; }

                Debug.Log("接続がなくなったのでサーバーを終了します");
                _ = await MainThreadDispatcher.RunAsync(async () =>
                {
                    await Task.Yield();
                    EditorApplication.isPlaying = false;
                    return "Editor Closed";
                });
#else
                _ = await MainThreadDispatcher.RunAsync(async () =>
                {
                    await Task.Yield();
                    Application.Quit();
                    return "Application Quit";
                });
#endif
            });
        }

        public IPAddress GetSelfIPAddress()
        {
            var hostName = Dns.GetHostName();
            IPAddress[] addressList = Dns.GetHostAddresses(hostName);
            foreach (var address in addressList)
            {
                if (address.ToString().Split(".")[0] == "10") { return address; }
            }

            return addressList[^1];
        }

        public string ReceiveGetRequest()
            => TryAddConnectCount() ? Success : Failed;

        /// <summary> IDのみをキーにして何か処理を行う場合 </summary>
        public async Task<string> ReceivePostRequest(string id, string requestMessage)
        {
            Debug.Log($"{id} {requestMessage}");
            return requestMessage switch
            {
                "CloseClient" => await CloseClient(id),
                "DeleteUserData" => await DeleteUserData(id),
                "GetName" => await GetName(id),
                "GetScore" => await GetScore(id),
                "GenerateID" => await GenerateID(),
                _ => ""
            };
        }

        /// <summary> ID以外のパラメータもキーにして何か処理を行う場合 </summary>
        public async Task<string> ReceivePutRequest(string data)
        {
            var parse = data.Split('^');
            var requestData = parse[0];
            var message = parse[1];

            Debug.Log($"Request : {message}");
            return message switch
            {
                "GetRanking" => await GetRanking(requestData),
                "GetUserData" => await GetUserData(requestData),
                "SetName" => await SetName(requestData),
                "SetScore" => await SetScore(requestData),
                _ => ""
            };
        }

        private async Task<string> CloseClient(string id)
        {
            //IDの重複がない → データがない、とする
            if (!_dataBaseManagers[0].IsDuplicate(id)) { return Failed; }

            RemoveConnectCount();
            await Task.Yield();
            return Success;
        }

        /// <summary> データの削除を行う </summary>
        /// <returns> 成功したら「Success」失敗したら「Failed」 </returns>
        private async Task<string> DeleteUserData(string id)
        {
            if (!TryGetDuplicateIndex(id, out int _)) { return Failed; }

            foreach (var db in _dataBaseManagers)
            {
                //途中で処理に失敗したらその時点で失敗
                if (!await db.DeleteData(id)) { return Failed; }
            }

            RemoveConnectCount();
            return Success;
        }

        /// <summary> IDの重複があるか調べる </summary>
        /// <param name="id"> 検索するID </param>
        /// <param name="index"> 結果のインデックス </param>
        /// <returns> 重複があったか、あった場合はそのインデックス </returns>
        private bool TryGetDuplicateIndex(string id, out int index)
        {
            //検索対象のデータがない場合は、検索しない
            if (_idList == null || _idList.Count == 0) { index = -1; return false; }

            index = _idList.IndexOf(id); return index >= 0;
        }

        private async Task<string> GetName(string id)
        {
            var resultName = "";
            foreach (var db in _dataBaseManagers)
            {
                if (resultName == "") { resultName = await db.GetData(id, "Name"); }
                else { break; }
            }
            return resultName;
        }

        private async Task<string> GetScore(string id)
        {
            var resultScore = "";
            foreach (var db in _dataBaseManagers)
            {
                if (resultScore == "") { resultScore = await db.GetData(id, "Score"); }
                else { break; }
            }
            return resultScore;
        }

        /// <summary> Scoreを参照し、上位いくつかのデータを取得する </summary>
        private async Task<string> GetRanking(string requestData)
        {
            var splitData = requestData.Split(',');
            //取得したい範囲の幅（1位 ～ 5位等）
            var from = int.Parse(splitData[1]);
            var to = int.Parse(splitData[2]);

            //RakigForat : Score1, Score2, ..., ScoreN
            string rankingMessage = "";
            int[] scores = (await _dataBaseManagers[0].GetDatas("Score")).Select(score => int.Parse(score)).ToArray();
            //降順ソート（スコア高い順に）
            Array.Sort(scores);
            Array.Reverse(scores);

            //取得したいデータ数が不足している場合はある分だけ
            var getRankingCount = scores.Length >= to ? to : scores.Length;

            for (int i = from - 1; i < getRankingCount; i++)
            {
                rankingMessage += scores[i];
                if (i < 4) { rankingMessage += ","; }
                await Task.Yield();
            }

            return rankingMessage;
        }

        /// <summary> IDからUserDataを取得する </summary>
        private async Task<string> GetUserData(string requestData)
        {
            var splitData = requestData.Split(',');
            var id = splitData[0];
            var targetClass = splitData[1];

            DataBaseManager targetDB = null;
            foreach (var db in _dataBaseManagers)
            {
                if (db.ClassName == targetClass)
                {
                    targetDB = db;
                    break;
                }
                await Task.Yield();
            }

            return await targetDB.GetData(id, targetClass);
        }

        private async Task<string> GenerateID()
        {
            var newID = await GenerateNewID();
            Debug.Log(newID);
            foreach (var db in _dataBaseManagers)
            {
                await db.GenerateNewData(newID);
            }
            return newID;
        }

        /// <summary> IDの新規生成 </summary>
        private async Task<string> GenerateNewID()
        {
            _random ??= new();
            string newID = "";
            await Task.Run(() =>
            {
                newID = new string(Enumerable.Repeat(CharLine, UserIDLength)
                                    .Select(s => s[_random.Next(s.Length)])
                                    .ToArray());
            });
            _idList ??= new();
            if (!_idList.Contains(newID)) { _idList.Add(newID); return newID; }
            else { return await GenerateNewID(); }
        }

        private async Task<string> SetName(string requestData)
        {
            var splitData = requestData.Split(',');
            var id = splitData[0];
            var name = splitData[1];

            foreach (var db in _dataBaseManagers)
            {
                var writeResult = await db.Write(id, $"Name {name}");
                Debug.Log($"WriteResult : {writeResult}");

                if (writeResult == Failed) { return Failed; }
            }
            return Success;
        }

        private async Task<string> SetScore(string requestData)
        {
            var splitData = requestData.Split(',');
            var id = splitData[0];
            var score = splitData[1];

            foreach (var db in _dataBaseManagers)
            {
                var writeResult = await db.Write(id, $"Score {score}");
                Debug.Log($"WriteResult : {writeResult}");

                if (writeResult == Failed) { return Failed; }
            }
            return Success;
        }

        private bool TryAddConnectCount()
        {
            if (_connectCount + 1 > _maxConcurrentConnectCount) { return false; }

            AddConnectCount();
            return true;
        }

        private void AddConnectCount() => ConnectCount++;

        private void RemoveConnectCount() => ConnectCount--;

        public int GetConnectCount() => _connectCount;

        public List<string[]> GetLatestDataTable(int index) => _dataBaseManagers[index].GetDataTable();
    }
}
