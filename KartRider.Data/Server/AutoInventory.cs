using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using ExcData;
using RiderData;

namespace KartRider
{
    /// <summary>
    /// 게임 데이터(Data / DataRaw)가 업데이트되어 새 아이템이 생기면 인벤토리에 자동으로 추가함.
    ///  - '새 아이템' = 지난번에 확인해 둔 목록(Profile\known_items.json)에 없던 아이템
    ///  - 인벤토리에 이미 있는 아이템은 건너뜀 (카트는 Profile\NewKart.xml, 나머지는 Profile\Item.xml 기준)
    ///  - 처음 실행할 때(기록 없음)는 현재 데이터를 기준으로 기록만 하고, 아무것도 추가하지 않음
    ///  - 대상 분류는 ItemCategories.Whitelist 의 분류 중 인벤토리 파일로 관리되는 것만
    /// 인벤토리 파일은 게임이 접속할 때 읽어 가므로, 이미 접속해 있는 게임에는 다시 접속하면 나타남.
    /// </summary>
    public static class AutoInventory
    {
        /// <summary>인벤토리 파일(NewKart.xml / Item.xml)을 고치는 동안 잡는 잠금 (아이템 추가 창과 동시에 고치지 않도록)</summary>
        public static readonly object FileLock = new object();

        private const string SnapshotPath = @"Profile\known_items.json";
        private static int running;

        // 분류 ID -> Profile\Item.xml 의 태그 (NewRider.cs 가 접속할 때 읽어서 인벤토리로 보내는 것과 같음). 카트(3)는 NewKart.xml 을 씀.
        private static readonly Dictionary<int, string> Tags = new Dictionary<int, string>
        {
            { 1, "Character" }, { 2, "Paint" }, { 4, "Plate" }, { 7, "SlotItem" }, { 8, "Goggle" }, { 9, "Balloon" },
            { 11, "HeadBand" }, { 12, "HeadPhone" }, { 13, "ReplayTicket" }, { 16, "HandGearL" }, { 18, "Uniform" },
            { 20, "Decal" }, { 21, "Pet" }, { 22, "InitialCard" }, { 23, "Card" }, { 26, "Aura" }, { 27, "SkidMark" },
            { 28, "MyRoom" }, { 31, "RidColor" }, { 32, "BonusCard" }, { 52, "FlyingPet" }, { 61, "Tachometer" },
            { 68, "V1EffectData" }, { 69, "V1BoosterEffectData" }, { 70, "Dye" }
        };

        private static bool Wants(int cat)
        {
            if (cat != 3 && !Tags.ContainsKey(cat)) return false;
            if (ItemCategories.Hidden.Contains(cat)) return false;
            return ItemCategories.Whitelist.Count == 0 || ItemCategories.Whitelist.Contains(cat);
        }

        private static string Label(int cat)
        {
            string n;
            if (ItemCategories.Names.TryGetValue(cat, out n) && !string.IsNullOrEmpty(n)) return n;
            return cat == 3 ? "카트" : (Tags.TryGetValue(cat, out n) ? n : "분류 " + cat);
        }

        /// <summary>데이터를 다 읽은 뒤(또는 설정을 켠 직후) 호출. 백그라운드에서 실행해도 됨</summary>
        public static void Run()
        {
            if (System.Threading.Interlocked.Exchange(ref running, 1) == 1) return;
            try
            {
                if (!LauncherSettings.Current.AutoAddNewItems) return;

                // 1) 지금 게임 데이터에 있는 아이템 (대상 분류만, 로그인 패킷이 short 로 읽으므로 32767 이하만)
                var current = new Dictionary<int, SortedSet<int>>();
                var names = new Dictionary<long, string>();
                lock (KartRhoFile.DataLock)
                {
                    foreach (KeyValuePair<ushort, Dictionary<ushort, string>> cat in new List<KeyValuePair<ushort, Dictionary<ushort, string>>>(NewRider.items))
                    {
                        if (!Wants(cat.Key) || cat.Key == 3) continue;
                        var set = new SortedSet<int>();
                        foreach (KeyValuePair<ushort, string> it in cat.Value)
                            if (it.Key > 0 && it.Key <= short.MaxValue) { set.Add(it.Key); names[((long)cat.Key << 32) | it.Key] = it.Value; }
                        current[cat.Key] = set;
                    }
                    if (Wants(3))
                    {
                        var karts = new SortedSet<int>();
                        foreach (KeyValuePair<int, KartTable> k in new List<KeyValuePair<int, KartTable>>(Kart.kartName))
                            if (k.Key > 0 && k.Key <= short.MaxValue) { karts.Add(k.Key); names[(3L << 32) | (uint)k.Key] = k.Value != null ? k.Value.Name : null; }
                        if (karts.Count > 0) current[3] = karts;
                    }
                }
                if (current.Count == 0) return;   // 데이터를 못 읽었으면 기록을 덮어쓰지 않음

                // 2) 지난번에 확인해 둔 목록과 비교
                Dictionary<int, HashSet<int>> known = LoadSnapshot();
                bool noSnapshot = known == null;
                if (noSnapshot) known = new Dictionary<int, HashSet<int>>();

                int totalAdded = 0, totalExisting = 0, baselined = 0;
                var summary = new List<string>();
                foreach (KeyValuePair<int, SortedSet<int>> kv in current)
                {
                    int cat = kv.Key;
                    HashSet<int> old;
                    if (!known.TryGetValue(cat, out old))
                    {
                        known[cat] = new HashSet<int>(kv.Value);   // 이 분류는 처음 보는 것: 기준만 기록
                        baselined++;
                        continue;
                    }
                    List<int> fresh = kv.Value.Where(id => !old.Contains(id)).ToList();
                    if (fresh.Count == 0) continue;

                    int added, existing;
                    try
                    {
                        lock (FileLock) { added = cat == 3 ? AddKarts(fresh, out existing) : AddItems(Tags[cat], fresh, out existing); }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[인벤토리] {0} 새 아이템 {1}개를 추가하지 못했어요 (다음에 다시 시도): {2}", Label(cat), fresh.Count, ex.Message);
                        continue;   // 기록에 반영하지 않아서 다음 번에 다시 시도함
                    }
                    foreach (int id in fresh) old.Add(id);
                    totalAdded += added;
                    totalExisting += existing;
                    if (added > 0)
                    {
                        var sample = fresh.Take(3).Select(id =>
                        {
                            string nm;
                            return names.TryGetValue(((long)cat << 32) | (uint)id, out nm) && !string.IsNullOrEmpty(nm) ? nm : ("#" + id);
                        });
                        summary.Add(Label(cat) + " " + added + "개 (예: " + string.Join(", ", sample) + ")");
                    }
                }
                SaveSnapshot(known);

                // 3) 결과 안내
                if (noSnapshot)
                    Console.WriteLine("[인벤토리] 현재 게임 데이터를 기준으로 기록했어요. 다음 업데이트부터 새 아이템을 인벤토리에 자동으로 추가해요");
                else if (totalAdded > 0)
                {
                    Console.WriteLine("[인벤토리] 게임 데이터의 새 아이템 {0}개를 인벤토리에 추가했어요 (이미 있어서 건너뜀 {1}개)", totalAdded, totalExisting);
                    foreach (string line in summary) Console.WriteLine("    {0}", line);
                    Console.WriteLine("[인벤토리] {0}", GameLink.IsConnected() ? "게임이 접속해 있어서, 다시 접속하면 인벤토리에 나타나요" : "다음에 게임에 접속하면 인벤토리에 나타나요");
                }
                else if (baselined == 0)
                    Console.WriteLine("[인벤토리] 새로 추가된 아이템이 없어요" + (totalExisting > 0 ? " (이미 인벤토리에 있는 새 아이템 " + totalExisting + "개는 건너뜀)" : ""));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[인벤토리] 새 아이템 확인 중 오류: " + ex.Message);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref running, 0);
            }
        }

        // ---------- 인벤토리 파일에 추가 ----------
        private static void BackupOnce(string path)
        {
            string bak = path + ".launcher-bak";
            if (!File.Exists(bak)) File.Copy(path, bak);
        }

        /// <summary>카트: Profile\NewKart.xml 에 없는 카트만 추가 (복사본 1대, sn=1) + 기본 파츠 정보 등록</summary>
        private static int AddKarts(List<int> ids, out int existing)
        {
            existing = 0;
            const string path = @"Profile\NewKart.xml";
            if (!File.Exists(path)) throw new FileNotFoundException("NewKart.xml 이 없어요");
            var doc = new XmlDocument();
            doc.Load(path);
            var have = new HashSet<int>();
            foreach (XmlNode xn in doc.GetElementsByTagName("Kart"))
            {
                int v;
                if (int.TryParse(((XmlElement)xn).GetAttribute("id"), out v)) have.Add(v);
            }
            var added = new List<int>();
            foreach (int id in ids)
            {
                if (have.Contains(id)) { existing++; continue; }
                XmlElement e = doc.CreateElement("Kart");
                e.SetAttribute("id", id.ToString());
                e.SetAttribute("sn", "1");
                doc.DocumentElement.AppendChild(e);
                added.Add(id);
            }
            if (added.Count == 0) return 0;
            BackupOnce(path);
            doc.Save(path);
            foreach (int id in added) KartExcData.AddPartsList((short)id, 1, 63, 0, 0, 0);   // 아이템 추가 창으로 지급할 때와 같은 기본 파츠 등록
            return added.Count;
        }

        /// <summary>그 밖의 아이템: Profile\Item.xml 에 없는 id 만 같은 태그로 추가 (서식과 주석은 그대로 둠)</summary>
        private static int AddItems(string tag, List<int> ids, out int existing)
        {
            existing = 0;
            const string path = @"Profile\Item.xml";
            if (!File.Exists(path)) throw new FileNotFoundException("Item.xml 이 없어요");
            byte[] raw = File.ReadAllBytes(path);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            int skip = bom ? 3 : 0;
            string text = new UTF8Encoding(false).GetString(raw, skip, raw.Length - skip);

            var have = new HashSet<int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "<" + tag + @"\s+id\s*=\s*['""](\d+)['""]"))
                have.Add(int.Parse(m.Groups[1].Value));

            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var sb = new StringBuilder();
            int added = 0;
            foreach (int id in ids)
            {
                if (have.Contains(id)) { existing++; continue; }
                sb.Append("\t<").Append(tag).Append(" id='").Append(id).Append("' />").Append(nl);
                added++;
            }
            if (added == 0) return 0;

            int close = text.LastIndexOf("</Item>", StringComparison.Ordinal);
            if (close < 0) throw new InvalidDataException("Item.xml 형식을 알 수 없어요");
            int lineStart = text.LastIndexOf('\n', close) + 1;
            bool ownLine = text.Substring(lineStart, close - lineStart).Trim().Length == 0;
            text = ownLine ? text.Insert(lineStart, sb.ToString()) : text.Insert(close, sb.ToString());

            BackupOnce(path);
            byte[] body = new UTF8Encoding(false).GetBytes(text);
            var outBytes = new byte[(bom ? 3 : 0) + body.Length];
            if (bom) { outBytes[0] = 0xEF; outBytes[1] = 0xBB; outBytes[2] = 0xBF; }
            Buffer.BlockCopy(body, 0, outBytes, bom ? 3 : 0, body.Length);
            File.WriteAllBytes(path, outBytes);
            return added;
        }

        // ---------- 확인해 둔 목록 ----------
        private static Dictionary<int, HashSet<int>> LoadSnapshot()
        {
            try
            {
                if (!File.Exists(SnapshotPath)) return null;
                var raw = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(SnapshotPath));
                if (raw == null) return null;
                var d = new Dictionary<int, HashSet<int>>();
                foreach (KeyValuePair<string, int[]> kv in raw)
                {
                    int c;
                    if (int.TryParse(kv.Key, out c) && kv.Value != null) d[c] = new HashSet<int>(kv.Value);
                }
                return d;
            }
            catch { return null; }   // 읽을 수 없으면 기록 없음으로 보고 기준만 다시 만듦 (아무것도 추가하지 않음)
        }

        private static void SaveSnapshot(Dictionary<int, HashSet<int>> known)
        {
            try
            {
                var raw = new Dictionary<string, int[]>();
                foreach (KeyValuePair<int, HashSet<int>> kv in known) raw[kv.Key.ToString()] = kv.Value.OrderBy(x => x).ToArray();
                File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(raw));
            }
            catch (Exception ex) { Console.WriteLine("[인벤토리] 확인 목록을 저장하지 못했어요: " + ex.Message); }
        }
    }
}
