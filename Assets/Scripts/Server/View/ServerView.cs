using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Network
{
    [Serializable]
    public class ServerView
    {
        [SerializeField]
        private Text _serverIPAddressText = default;
        [SerializeField]
        private Text _connectionCount = default;
        [SerializeField]
        private Text _responseString = default;
        [SerializeField]
        private Text _dataBaseSheetText = default;

        public void GetServerIPAddress(string address)
        {
            if (_serverIPAddressText == null) { return; }
            _serverIPAddressText.text = address;
        }

        public void UpdateConnectCount(int count)
        {
            if (_connectionCount == null) { return; }
            _connectionCount.text = count.ToString();
        }

        public void UpdateResponseString(string response)
        {
            if (_responseString == null) { return; }
            _responseString.text = response;
        }
        
        public void UpdateDB(List<string[]> dataTable)
        {
            if (_dataBaseSheetText == null) { return; }

            _dataBaseSheetText.text = "";
            foreach (var personalData in dataTable)
            {
                for (int i = 0; i < personalData.Length; i++)
                {
                    _dataBaseSheetText.text +=
                        personalData[i].ToString() + (i == personalData.Length - 1 ? "\n" : ", ");
                }
            }
        }
    }
}
