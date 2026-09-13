using System;

public interface INetworkTransport
{
    bool IsConnect {get ;}

    bool Connect(string _ip,int _port,string _playerName);
    void Close();
    void SendRaW(byte[] _data);
    void ApplyResumedIdentity(int _playerId,int _seatId);
    void ResetLocalSession();
}