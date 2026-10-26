using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace HelloWorld
{
    [Serializable] public sealed class DuelRoomAdmission
    {
        public string room,nickname,proof;
        public static string PasswordProof(string room,string password)
        {
            if(string.IsNullOrEmpty(password))return "";
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(room+":"+password.Normalize()))).Replace("-","").ToLowerInvariant();
        }
        public static byte[] Encode(string room,string nickname,string proof)=>Encoding.UTF8.GetBytes(JsonUtility.ToJson(new DuelRoomAdmission{room=room,nickname=NicknamePolicy.Normalize(nickname),proof=proof}));
        public static bool TryRead(byte[] bytes,string expectedRoom,string expectedProof,out string nickname,out string error)
        {
            nickname="";error="방 참가 정보가 올바르지 않습니다.";
            if(bytes==null||bytes.Length>512)return false;
            try
            {
                var value=JsonUtility.FromJson<DuelRoomAdmission>(new UTF8Encoding(false,true).GetString(bytes));
                if(value==null||value.room!=expectedRoom||value.nickname==null||Encoding.UTF8.GetByteCount(value.nickname)>NicknamePolicy.MaxBytes)return false;
                string proof=value.proof??"";int difference=proof.Length^expectedProof.Length;
                for(int i=0;i<expectedProof.Length;i++)difference|=expectedProof[i]^(i<proof.Length?proof[i]:0);
                if(difference!=0){error="비밀번호가 올바르지 않습니다.";return false;}
                nickname=NicknamePolicy.Normalize(value.nickname);error="";return true;
            }
            catch(Exception e)when(e is ArgumentException||e is DecoderFallbackException){return false;}
        }
    }
}
