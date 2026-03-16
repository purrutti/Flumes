using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SuperviFlume_v2
{
    // ── Trame d'identification reçue en premier pour router le message ──────────
    public class TrameJson
    {
        [JsonProperty("cmd",    Required = Required.Default)] public int cmd   { get; set; }
        [JsonProperty("AquaID", Required = Required.Default)] public int ID    { get; set; }
        [JsonProperty("PLCID",  Required = Required.Default)] public int PLCID { get; set; }
    }

    // ── Données du circuit général (cmd 6 – envoyé par le MasterFlume) ─────────
    public class MasterData
    {
        [JsonProperty("cmd",  Required = Required.Default)] public int           Command { get; set; }
        [JsonProperty("PLCID",Required = Required.Default)] public int           PLCID   { get; set; }
        [JsonProperty("data", Required = Required.Default)] public List<DataItem> Data   { get; set; }
        [JsonProperty("time", Required = Required.Default)] public long           Time   { get; set; }
    }

    // Un circuit hydraulique dans MasterData (temp / pression / débit + réguls)
    public class DataItem
    {
        [JsonProperty("CondID",    Required = Required.Default)] public int    ConditionID { get; set; }
        [JsonProperty("temp",      Required = Required.Default)] public double Temperature { get; set; }
        [JsonProperty("pH",        Required = Required.Default)] public double PH          { get; set; }
        [JsonProperty("pression",  Required = Required.Default)] public double Pression    { get; set; }
        [JsonProperty("debit",     Required = Required.Default)] public double Debit       { get; set; }
        [JsonProperty("rTemp",     Required = Required.Default)] public Regul  RTemp       { get; set; }
        [JsonProperty("rPression", Required = Required.Default)] public Regul  RPression   { get; set; }
    }

    // ── Aquarium (cmd 2 / 3 – envoyé par chaque automate aquarium) ────────────
    public class Aquarium
    {
        [JsonProperty("AquaID",     Required = Required.Default)] public int    ID           { get; set; }
        [JsonProperty("PLCID",      Required = Required.Default)] public int    PLCID        { get; set; }
        [JsonProperty("control",    Required = Required.Default)] public bool   control      { get; set; }
        [JsonProperty("debit",      Required = Required.Default)] public double debit        { get; set; }
        [JsonProperty("debitCircul",Required = Required.Default)] public double debitCircul  { get; set; }
        [JsonProperty("temp",       Required = Required.Default)] public double temperature  { get; set; }
        [JsonProperty("pH",         Required = Required.Default)] public double pH           { get; set; }
        [JsonProperty("oxy",        Required = Required.Default)] public double oxy          { get; set; }
        [JsonProperty("rTemp",      Required = Required.Default)] public Regul  regulTemp    { get; set; }
        [JsonProperty("rpH",        Required = Required.Default)] public Regul  regulpH      { get; set; }
        public long     time        { get; set; }
        public DateTime lastUpdated { get; set; }
    }

    // ── Bloc de régulation PID (partagé par Aquarium et DataItem) ─────────────
    public class Regul
    {
        [JsonProperty(Required = Required.Default)]            public double sortiePID          { get; set; }
        [JsonProperty("cons",    Required = Required.Default)] public double consigne           { get; set; }
        [JsonProperty(Required = Required.Default)]            public double Kp                 { get; set; }
        [JsonProperty(Required = Required.Default)]            public double Ki                 { get; set; }
        [JsonProperty(Required = Required.Default)]            public double Kd                 { get; set; }
        [JsonProperty("sPID_pc", Required = Required.Default)] public double sortiePID_pc       { get; set; }
        [JsonProperty(Required = Required.Default)]            public bool   autorisationForcage { get; set; }
        [JsonProperty(Required = Required.Default)]            public int    consigneForcage     { get; set; }
        [JsonProperty(Required = Required.Default)]            public double offset              { get; set; }
    }
}
