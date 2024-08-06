using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Network
{
    namespace DB
    {
        public class FileCreator
        {
            /// <summary> 指定パスにファイルの生成を行う </summary>
            /// <typeparam name="T"> 保存するデータの型 </typeparam>
            /// <param name="path"> データテーブルのパス </param>
            public void Create<T>(string path) where T : class
            {
                //既にファイルが存在したら何もしない
                if (File.Exists(path)) { Debug.Log("already exists"); return; }

                //ファイルが存在しなかった場合は生成し、初期データを設定して保存
                using (File.Create(path)) { }

                //以下クラスのPublic変数を取得し、初期データとして設定する処理
                Type type = typeof(T);
                var fields = new List<FieldInfo>();
                while (type != null && type != typeof(object))
                {
                    // 基底クラスのフィールドを先に追加するため、前に挿入
                    fields.InsertRange(0, type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    type = type.BaseType;
                }

                string defaultDataName = "";
                for (int i = 0; i < fields.Count; i++)
                {
                    defaultDataName += fields[i].Name;
                    if (i < fields.Count - 1) { defaultDataName += ","; }
                }

                using (StreamWriter writer = new(path))
                {
                    writer.Write(defaultDataName);
                }
#if UNITY_EDITOR
                AssetDatabase.Refresh();
#endif
            }
        }
    }
}
