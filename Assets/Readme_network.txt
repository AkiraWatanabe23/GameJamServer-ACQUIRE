==ServerProgram の使い方==

開発時実行環境：2022.3.14f1
サンプルシーン：「Assets/Scenes/ServerDemoScene.unity」


1, ServerRunner.cs
  ・クライアントからのリクエスト待機、受け取ったリクエストに対する処理を ServerModel に渡す
  ・ServerModel からの結果をクライアントに返却する
  ・「Assets/Prefabs/ServerRunner.prefab」があるので、シーン上に配置して使用する


2, ServerView.cs
  ・サーバーが保持しているデータをシーン上で確認するためのUIを管理する
  ・デフォルトでは、サーバー機のIPAddress、クライアントの接続数が表示される
  ・「Asstes/Prefabs/ServerView.prefab」にUIがまとめられている
  ・必要があれば編集する


3, ServerModel.cs
  ・クライアントからのリクエストに対する処理を記述する
  ・追記、編集を行うクラスは基本的にこのクラス
  ・「public async Task<string> ReceivePostRequest(string id, string requestMessage);」
    「public async Task<string> ReceivePutRequest(string data)」のいずれかにリクエストによる分岐を記述

    <<違い>>
    ・ReceivePostRequest → IDを用いて「データの読み取り」を行いたい場合はこちら（ex. 自分のスコアを取得したい）
    ・ReceivePutRequest  → 「データの書き込み」を行いたい場合はこちら（ex. 自分のスコアをサーバーに保存したい）

  ・関数は新規に作成する
    ※クライアントに返還するデータの形式の都合上、戻り値が string型のものとする


4, DataBaseManager.cs
  ・クライアントのデータを保持する
  ・ServerModel.cs と紐づけられている
  ・Model はクライアントからのリクエストを受けて、このクラス内のデータを参照する