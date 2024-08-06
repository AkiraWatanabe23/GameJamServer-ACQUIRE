using Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Network
{
    namespace DB
    {
        /// <summary> サーバーで扱うデータを管理するクラス </summary>
        [Serializable]
        public class DataBaseManager
        {
            [Tooltip("シートに保存するデータのクラス名")]
            [AbstractClass(typeof(AbstractData))]
            [SerializeField]
            private string _className = "";
            [SerializeField]
            private string _sheetName = "SampleSheet";

            private TextAsset _csvFile = default;
            private string _csvPath = default;
            /// <summary> csvファイルに書き込む用のデータ </summary>
            private List<string[]> _dataTable = default;
            /// <summary> データとして保持するDictionary </summary>
            private Dictionary<string, object> _userDataDict = default;

            /// <summary> リクエストに対する一連の処理が正常に流れた時に返す文字列 </summary>
            private const string Success = "Request Success";
            /// <summary> リクエストに対する一連の処理が失敗した時に返す文字列 </summary>
            private const string Failed = "Request Failed";

            protected Type ClassType => Type.GetType(_className);

            public string ClassName => _className;

            #region Init
            /// <summary> 初期化関数 </summary>
            public async Task Initialize()
            {
                var load = Resources.LoadAsync<TextAsset>(_sheetName);
                while (!load.isDone) { await Task.Yield(); }

                if (load.asset != null) { _csvFile = load.asset as TextAsset; }
                else
                {
                    FileCreator fileCreator = new();

                    //リフレクションを用いたジェネリックメソッドの実行
                    //「どのクラスの」「どの関数か」を指定
                    MethodInfo methodInfo = typeof(FileCreator).GetMethod("Create");
                    //ジェネリックの関数を実行する型を指定
                    MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(Type.GetType(_className));
                    //引数がある場合、object[]で用意する
                    object[] parameters = new object[] { $"Assets/Resources/{_sheetName}.csv" };
                    //実行
                    genericMethodInfo.Invoke(fileCreator, parameters);

                    _csvFile = Resources.Load<TextAsset>(_sheetName);
                }
#if UNITY_EDITOR
                _csvPath = AssetDatabase.GetAssetPath(_csvFile);
#endif
                Load();
            }

            /// <summary> データの読み込み </summary>
            private async void Load()
            {
                _userDataDict ??= new();
                _dataTable ??= new();
                if (_dataTable.Count > 0) { _dataTable.Clear(); }

                using StreamReader reader = new(_csvPath, Encoding.UTF8);
                var readLine = "";
                for (int i = 0; (readLine = reader.ReadLine()) != null; i++)
                {
                    var split = readLine.Split(',');
                    _dataTable.Add(split);

                    if (i == 0) { await Task.Yield(); continue; }

                    var id = split[0];
                    //新規インスタンスの生成
                    _userDataDict.Add(id, Activator.CreateInstance(ClassType));
                    ClassType.BaseType.GetField("UserID", BindingFlags.Public | BindingFlags.Instance).SetValue(_userDataDict[id], id);

                    for (int j = 1; j < split.Length; j++)
                    {
                        if (int.TryParse(split[j], out int intValue))
                        {
                            ClassType.GetField(_dataTable[0][j], BindingFlags.Public | BindingFlags.Instance)
                                .SetValue(_userDataDict[id], intValue);
                        }
                        else
                        {
                            ClassType.GetField(_dataTable[0][j], BindingFlags.Public | BindingFlags.Instance)
                                .SetValue(_userDataDict[id], split[1]);
                        }
                    }
                    await Task.Yield();
                }
            }
            #endregion

            /// <summary> データ群の取得 </summary>
            public List<string[]> GetDataTable() => _dataTable;

            /// <summary> インスタンスの新規作成 </summary>
            public async Task GenerateNewData(string newID)
            {
                _userDataDict ??= new();
                _userDataDict.Add(newID, Activator.CreateInstance(ClassType));

                //ここでシートに対する書き込み処理を実行する
                await CreateWrite(newID);

                Debug.Log($"Generate new UserID : {newID}");
            }

            /// <summary> データの取得 </summary>
            /// <param name="id"> 検索するUserID </param>
            /// <param name="paramNames"> 取得したいデータ名 </param>
            /// <returns> 検索結果 </returns>
            public async Task<string> GetData(string id, params string[] paramNames)
            {
                object result = null;
                await Task.Run(() =>
                {
                    foreach (var key in _userDataDict.Keys)
                    {
                        //見つかったらそのデータを返す
                        if (key == id)
                        {
                            result = _userDataDict[key];
                            break;
                        }
                    }
                });
                if (result == null) { Debug.LogError($"Data not found : {id}"); return ""; }

                //取得したいパラメータ名にクラス名が渡された → 指定クラスのメンバ変数を全て返す
                if (paramNames.Length == 1 && _className == paramNames[0])
                {
                    var returnParams = "";
                    FieldInfo[] fields = ClassType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    for (int i = 0; i < fields.Length - 1; i++)
                    {
                        returnParams +=
                            $"{fields[i].Name}:{ClassType.GetField(fields[i].Name).GetValue(result)}";
                        if (i != fields.Length - 2) { returnParams += ","; }
                    }
                    Debug.Log(returnParams);
                    return returnParams;
                }
                else if (paramNames.Length == 1) //特定のパラメータが指定された
                {
                    // 指定された名前のフィールドを取得
                    return ClassType.GetField(paramNames[0], BindingFlags.Public | BindingFlags.Instance).GetValue(result).ToString();
                }
                else //複数のパラメータが渡された（全部ではない場合）
                {
                    var returnParams = "";
                    for (int i = 0; i < paramNames.Length; i++)
                    {
                        returnParams +=
                            ClassType.GetField(paramNames[i], BindingFlags.Public | BindingFlags.Instance).GetValue(result).ToString();
                        if (i != paramNames.Length - 1) { returnParams += ","; }
                    }
                    Debug.Log(returnParams);
                    return returnParams;
                }
            }

            /// <summary> データの取得 </summary>
            /// <param name="paramName"> 取得したいデータ名 </param>
            /// <returns> 検索結果 </returns>
            public async Task<string[]> GetDatas(string paramName)
            {
                //IDを必要とせず、とりあえず一括でデータが欲しいときとかに
                var dataList = new List<string>();

                foreach (var key in _userDataDict.Keys)
                {
                    dataList.Add(
                        ClassType.GetField(paramName, BindingFlags.Public | BindingFlags.Instance)
                        .GetValue(_userDataDict[key]).ToString());
                    await Task.Yield();
                }
                return dataList.ToArray();
            }

            /// <summary> データの削除 </summary>
            /// <param name="id"> 検索するUserID </param>
            /// <returns> 削除に成功したか </returns>
            public async Task<bool> DeleteData(string id)
            {
                //見つかったら、各シートに対して削除を実行
                var index = _dataTable.FindIndex(array => array[0] == id);

                _dataTable.RemoveAt(index);
                _userDataDict.Remove(id);

                //削除したデータをシートに反映する
                using StreamWriter writer = new(_csvPath, false, Encoding.UTF8);
                foreach (string[] row in _dataTable)
                {
                    await writer.WriteLineAsync(string.Join(",", row));
                }
                return true;
            }

            /// <summary> IDの重複があるか調べる </summary>
            /// <returns> 重複があればtrue </returns>
            public bool IsDuplicate(string key)
            {
                //検索対象のデータがない場合は、検索しない
                if (_userDataDict == null || _userDataDict.Count == 0) { return false; }

                //二分探索で探す
                _userDataDict = _userDataDict.OrderBy(user => user.Key).ToDictionary(key => key.Key, value => value.Value);
                var searchIDList = _userDataDict.Keys.ToList();
                var min = 0;
                var max = _userDataDict.Count - 1;
                while (true)
                {
                    if (max < min) { return false; }

                    var middle = min + (max - min) / 2;
                    //文字列の代償を比較する
                    switch (key.CompareTo(searchIDList[middle]))
                    {
                        case -1: max = middle - 1; break;
                        case 0: return true;
                        case 1: min = middle + 1; break;
                    }
                }
            }

            private object GetDefaultValue(Type type)
            {
                if (type == typeof(string)) { return "sample"; }
                else if (type == typeof(int)) { return 0; }

                return null;
            }

            #region Sheet Update
            /// <summary> データの書き込み </summary>
            /// <param name="writeData"> 書き込むデータ </param>
            public async Task<string> Write(string id, string writeData)
            {
                //更新対象の型が設定されてなかったら無視
                if (ClassType == null) { return Failed; }

                foreach (var tableElement in _dataTable)
                {
                    // IDが一致する行のデータを更新
                    if (tableElement[0] == id)
                    {
                        var data = writeData.Split();
                        var targetParam = data[0];
                        var value = data[1];
                        var index = Array.IndexOf(_dataTable[0], targetParam);
                        if (index < 0) { return "Parameter not found"; }

                        tableElement[index] = value;

                        //型の確認
                        if (int.TryParse(value, out int intValue))
                        {
                            ClassType.GetField(targetParam, BindingFlags.Public | BindingFlags.Instance)
                                .SetValue(_userDataDict[id], intValue);
                        }
                        else
                        {
                            ClassType.GetField(targetParam, BindingFlags.Public | BindingFlags.Instance)
                                .SetValue(_userDataDict[id], value);
                        }
                        break;
                    }
                }

                // CSVファイルに書き込み
                using StreamWriter writer = new(_csvPath, false, Encoding.UTF8);
                foreach (string[] row in _dataTable)
                {
                    await writer.WriteLineAsync(string.Join(",", row));
                }
                return Success;
            }

            /// <summary> データの新規書き込み </summary>
            /// <returns> 書き込みに成功したか </returns>
            private async Task<bool> CreateWrite(string id)
            {
                //対象シートに対応したデータを調べ、初期値を割り当てる
                var type = Type.GetType(_className);
                if (type == null) { Debug.LogError($"{_className} not found"); return false; }

                var fields = new List<FieldInfo>();
                while (type != null && type != typeof(object))
                {
                    // 基底クラスのフィールド（今回はID）を先に追加するため、前に挿入
                    fields.InsertRange(0, type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    type = type.BaseType;
                }
                var sampleData = new List<string>();
                for (int i = 0; i < fields.Count; i++)
                {
                    if (i == 0) { sampleData.Add(id); }
                    else { sampleData.Add(GetDefaultValue(fields[i].FieldType).ToString()); }
                }
                _dataTable ??= new();
                _dataTable.Add(sampleData.ToArray());

                // CSVファイルに書き込み
                Debug.Log(_csvPath);
                using StreamWriter writer = new(_csvPath, false, Encoding.UTF8);
                foreach (string[] row in _dataTable)
                {
                    await writer.WriteLineAsync(string.Join(",", row));
                }
                return true;
            }
            #endregion
        }
    }
}
