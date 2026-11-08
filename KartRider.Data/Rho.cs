using KartLibrary.Data;
using KartLibrary.File;
using KartLibrary.IO;
using KartLibrary.Xml;
using RiderData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace KartRider
{
    /// <summary>
    /// 게임 Data\aaa.pk (.rho/.rho5)에서 카트·펫·아이템·콘텐츠 데이터를 읽어 옴 (V2/P3563 Rho.cs 방식).
    /// - 읽기 전용: 게임 파일(aaa.pk 등)은 수정하지 않음 (사운드/BGM 등록 처리는 넣지 않음)
    /// - 못 찾은 파일/파싱 실패는 콘솔에 남기고 건너뜀 -> 해당 카트·펫은 기본 성능으로 처리됨
    /// </summary>
    public static class KartRhoFile
    {
        public static string regionCode = "cn";
        public static volatile bool Loaded = false;
        /// <summary>런처 화면 표시용: 대기 / 읽는 중 / 완료 / 실패 / 없음</summary>
        public static volatile string Status = "대기";

        /// <summary>경로 -> 보관 이름. 퀘스트/상점/이벤트 등 원본 문서는 XmlDocument 그대로 보관</summary>
        public static readonly Dictionary<string, XmlDocument> Docs = new Dictionary<string, XmlDocument>();

        /// <summary>게임 데이터(itemTable)의 분류명(태그 이름: kart, balloon, headBand ...) -> 그 분류에 속한 아이템 id 들. 아이템 추가 창에서 분류 ID 와 짝지어 표시하는 데 사용</summary>
        public static readonly Dictionary<string, HashSet<int>> ItemTableTags = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> BuildDocPaths(string r)
        {
            var d = new Dictionary<string, string>
            {
                { $"zeta/{r}/quest/QuestAutomation.bml", "QuestAutomation" },
                { $"zeta/{r}/quest/kartPassQuestAutomation.bml", "kartPassQuestAutomation" },
                { $"zeta/{r}/scenario/scenario.bml", "scenario" },
                { "item/slot/itemProb_indi@zz.bml", "itemProb_indi" },
                { "item/slot/itemProb_team@zz.bml", "itemProb_team" },
                { $"zeta_/{r}/content/basicAI.xml", "basicAI" },
                { $"zeta_/{r}/content/bingoGachaSet.xml", "bingoGachaSet" },
                { $"zeta_/{r}/content/itemDictionary.xml", "itemDictionary" },
                { $"zeta_/{r}/content/timeAttack/timeAttackMission.xml", "timeAttackMission" },
                { $"zeta_/{r}/content/timeAttack/timeAttackCompetitive.xml", "timeAttackCompetitive" },
                { $"zeta_/{r}/content/timeAttack/timeAttackCompetitiveData.xml", "timeAttackCompetitiveData" },
                { $"zeta_/{r}/lottery/lottery.xml", "lottery" },
                { $"zeta_/{r}/shop/data/stock.kml", "stock" },
                { $"zeta_/{r}/shop/data/item.kml", "shopItem" },
                { $"zeta_/{r}/content/channel.xml", "channel" },
                { $"zeta_/{r}/content/mix/mixTable.xml", "mixTable" },
                { $"etc_/emblem/emblem@{r}.xml", "emblem" },
                { $"etc_/riderSchool/riderSchoolLocale@{r}.xml", "riderSchoolLocale" },
            };
            // 트랙 데이터: track/ 또는 track_/, .bml 또는 .xml 모두 허용 (V2와 동일)
            foreach (var dir in new[] { "track", "track_" })
                foreach (var ext in new[] { "bml", "xml" })
                {
                    d[$"{dir}/common/randomTrack@{r}.{ext}"] = "randomTrack";
                    d[$"{dir}/common/track@zz.{ext}"] = "track";
                    d[$"{dir}/common/trackLocale@{r}.{ext}"] = "trackLocale";
                }
            return d;
        }

        /// <summary>백그라운드 스레드로 로드 시작 (런처 창이 멈추지 않도록)</summary>
        public static void StartLoad(string aaaPkPath)
        {
            Console.WriteLine("[Rho] 게임 데이터 읽기: 아이템·카트·펫 스펙 (항상), 퀘스트·상점·이벤트 문서={0}", LauncherSettings.Current.LoadContentDocs);
            if (!System.IO.File.Exists(aaaPkPath))
            {
                Console.WriteLine("[Rho] aaa.pk 없음 -> 카트·펫 스펙을 읽을 수 없어 기본 성능을 사용해요: " + aaaPkPath);
                Status = "없음";
                try
                {
                    string dataDir = System.IO.Path.GetDirectoryName(aaaPkPath);
                    if (System.IO.Directory.Exists(dataDir))
                        Console.WriteLine("[Rho] Data 폴더 내용(최대 60개): " + string.Join(", ", System.IO.Directory.GetFileSystemEntries(dataDir).Select(x => System.IO.Path.GetFileName(x)).Take(60)));
                    else
                        Console.WriteLine("[Rho] Data 폴더 자체가 없음: " + dataDir);
                }
                catch { }
                return;
            }
            Status = "읽는 중";
            var t = new Thread(() => Dump(aaaPkPath)) { IsBackground = true, Name = "RhoDump" };
            t.Start();
        }

        // ===================================================================
        //  데이터를 읽는 방식
        //   - 기존: Data 폴더의 aaa.pk (rho/rho5 묶음 파일)
        //   - KartRider.xml 에 <datapackOff path='DataRaw' .../> 가 있으면: 그 폴더(이미 풀려 있는 파일들)에서 읽음
        // ===================================================================

        public enum DataSourceKind { Pack, Folder }

        public sealed class GameDataSource
        {
            public DataSourceKind Kind;
            /// <summary>Pack: aaa.pk 경로 / Folder: 풀려 있는 데이터 폴더의 전체 경로</summary>
            public string Location;
            /// <summary>로그에 보여줄 설명 (왜 이 방식이 선택됐는지)</summary>
            public string Message;
        }

        /// <summary>true: datapackOff 폴더에서 읽는 중 (화면 표시용)</summary>
        public static volatile bool FromFolder = false;

        // 폴더 방식: 읽은 폴더와, 카트/펫 이름별 param 파일이 있던 폴더 (주행 시작 때 다시 읽기용)
        private static string rawRoot;
        private static readonly Dictionary<string, string> rawKartDirs = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> rawPetDirs = new Dictionary<string, string>();

        /// <summary>KartRider.xml 의 datapackOff path 를 읽음 (없으면 null). 주석 처리된 항목은 무시됨</summary>
        private static string ReadDatapackOffPath(string xmlPath, out string note)
        {
            note = null;
            if (!File.Exists(xmlPath)) { note = "KartRider.xml 없음"; return null; }
            try
            {
                XDocument doc = XDocument.Load(xmlPath);
                foreach (XElement el in doc.Descendants())
                {
                    if (!string.Equals(el.Name.LocalName, "datapackOff", StringComparison.OrdinalIgnoreCase)) continue;
                    XAttribute pa = el.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase));
                    if (pa == null || string.IsNullOrWhiteSpace(pa.Value)) { note = "KartRider.xml 의 datapackOff 에 path 가 없음"; return null; }
                    return pa.Value.Trim();
                }
                note = "KartRider.xml 에 datapackOff 항목 없음";
                return null;
            }
            catch (Exception ex)
            {
                // XML 형식이 완전하지 않아 읽지 못하면 글자 검색으로 한 번 더 시도
                try
                {
                    var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(xmlPath), @"<datapackOff\b[^>]*?\bpath\s*=\s*['""]([^'""]+)['""]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
                catch { }
                note = "KartRider.xml 읽기 실패: " + ex.Message;
                return null;
            }
        }

        /// <summary>어느 방식으로 읽을지 결정 (게임 폴더 = 런처가 있는 폴더)</summary>
        public static GameDataSource ResolveSource(string gameDir)
        {
            string aaa = Path.Combine(gameDir, "Data", "aaa.pk");
            string note;
            string raw = ReadDatapackOffPath(Path.Combine(gameDir, "KartRider.xml"), out note);
            if (raw == null)
                return new GameDataSource { Kind = DataSourceKind.Pack, Location = aaa, Message = note };

            string full;
            try { full = Path.IsPathRooted(raw) ? raw : Path.GetFullPath(Path.Combine(gameDir, raw)); }
            catch { full = raw; }
            if (Directory.Exists(full))
                return new GameDataSource { Kind = DataSourceKind.Folder, Location = full, Message = "datapackOff path='" + raw + "' → " + full };
            return new GameDataSource
            {
                Kind = DataSourceKind.Pack,
                Location = aaa,
                Message = "KartRider.xml 에 datapackOff path='" + raw + "' 가 있지만 폴더를 찾을 수 없어 기존 방식으로 읽어요 (" + full + ")"
            };
        }

        /// <summary>결정된 방식으로 백그라운드 로드 시작</summary>
        public static void StartLoad(GameDataSource src)
        {
            CurrentSource = src;
            if (src.Kind == DataSourceKind.Folder)
            {
                FromFolder = true;
                Console.WriteLine("[Rho] 게임 데이터 읽기 (폴더 방식): 아이템·카트·펫 스펙 (항상), 퀘스트·상점·이벤트 문서={0}", LauncherSettings.Current.LoadContentDocs);
                Status = "읽는 중";
                var t = new Thread(() => DumpFolder(src.Location)) { IsBackground = true, Name = "RhoDumpFolder" };
                t.Start();
                return;
            }
            FromFolder = false;
            StartLoad(src.Location);
        }

        private static bool IsRegionCode(string n)
        {
            return n != null && n.Length == 2 && char.IsLetter(n[0]) && char.IsLetter(n[1]);
        }

        /// <summary>풀린 폴더에서 국가 리전 찾기: zeta/&lt;xx&gt; 폴더 → zeta_&lt;xx&gt; 이름 → itemTable@&lt;xx&gt;.xml. 알 수 없으면 null</summary>
        public static string DetectRegionRaw(string root)
        {
            try
            {
                foreach (string zetaName in new[] { "zeta", "zeta_" })
                {
                    string zeta = Path.Combine(root, zetaName);
                    if (!Directory.Exists(zeta)) continue;
                    foreach (string d in Directory.GetDirectories(zeta))
                    {
                        string n = Path.GetFileName(d);
                        if (IsRegionCode(n)) return n.ToLowerInvariant();
                    }
                }
                foreach (string e in Directory.GetFileSystemEntries(root))
                {
                    string n = Path.GetFileNameWithoutExtension(Path.GetFileName(e));   // zeta_kr.rho / zeta_kr 폴더
                    if (n.StartsWith("zeta_", StringComparison.OrdinalIgnoreCase) && IsRegionCode(n.Substring(5))) return n.Substring(5).ToLowerInvariant();
                }
                foreach (string f in EnumerateFilesSafe(root))
                {
                    string n = Path.GetFileName(f);
                    if (n.StartsWith("itemTable@", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        string code = n.Substring(10, n.Length - 14);
                        if (IsRegionCode(code)) return code.ToLowerInvariant();
                    }
                }
            }
            catch { }
            return null;
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files = null, dirs = null;
                try { files = Directory.GetFiles(dir); dirs = Directory.GetDirectories(dir); }
                catch { }
                if (files != null) foreach (string f in files) yield return f;
                if (dirs != null) foreach (string d in dirs) stack.Push(d);
            }
        }

        /// <summary>폴더 방식: 풀린 파일을 직접 읽음 (기존 Dump 와 같은 결과를 Kart/FlyingPet/NewRider 에 채움)</summary>
        public static void DumpFolder(string root)
        {
            try
            {
                Console.WriteLine("[Rho] 게임 데이터 읽는 중 (폴더): " + root);
                rawRoot = root;
                string detected = DetectRegionRaw(root);
                if (detected == null) Console.WriteLine("[Rho] 폴더에서 국가 리전을 찾지 못해 kr 로 가정해요");
                regionCode = detected ?? "kr";
                Console.WriteLine("[Rho] regionCode = " + regionCode);

                var paths = BuildDocPaths(regionCode);
                var docKeys = paths.Select(kv => new KeyValuePair<string, string>(NormPath(kv.Key), kv.Value)).ToList();
                var docFileNames = new HashSet<string>(paths.Keys.Select(k => k.Substring(k.LastIndexOf('/') + 1).ToLowerInvariant()));
                var kartParamPri = new Dictionary<string, int>();
                int fileCount = 0, failCount = 0;
                foreach (string file in EnumerateFilesSafe(root))
                {
                    fileCount++;
                    string lower = Path.GetFileName(file).ToLowerInvariant();
                    // 필요한 파일만 열어 봄 (카트/펫 param, itemTable, 상점·문서 파일)
                    if (!(lower.StartsWith("param") || lower.StartsWith("itemtable") || docFileNames.Contains(lower))) continue;
                    try { HandleRawFile(root, file, paths, docKeys, kartParamPri); }
                    catch (Exception ex)
                    {
                        failCount++;
                        Console.WriteLine($"[Rho] 건너뜀 {file}: {ex.Message}");
                    }
                }

                int itemCount = NewRider.items.Values.Sum(v => v.Count);
                Console.WriteLine($"[Rho] 파일 {fileCount}개 검사, 실패 {failCount}개");
                Console.WriteLine($"[Rho] 카트 이름 {Kart.kartName.Count}개, 카트 스펙 {Kart.kartSpec.Count}개, 펫 이름 {FlyingPet.flyingName.Count}개, 펫 스펙 {FlyingPet.flyingSpec.Count}개, 상점 아이템 {itemCount}개");
                if (Kart.kartSpec.Count == 0)
                    Console.WriteLine("[Rho] 카트 스펙을 하나도 찾지 못했어요. 폴더 구조가 kart_/<이름>/param.xml 형태인지 확인해 주세요: " + root);
                if (LauncherSettings.Current.LoadContentDocs)
                {
                    var missing = paths.Values.Distinct().Where(k => !Docs.ContainsKey(k)).ToList();
                    Console.WriteLine("[Rho] 읽은 문서: " + string.Join(", ", Docs.Keys));
                    if (missing.Count > 0) Console.WriteLine("[Rho] 못 찾은 문서(구버전에 없을 수 있음): " + string.Join(", ", missing));
                }
                Loaded = true;
                Status = "완료";
                Console.WriteLine("[Rho] 로드 완료 (폴더 방식)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Rho] 폴더 로드 실패 -> 카트·펫 스펙은 기본 성능으로 처리돼요: " + ex);
                Status = "실패";
            }
        }

        private static string StripArchiveExt(string seg)
        {
            if (seg.EndsWith(".rho5", StringComparison.OrdinalIgnoreCase)) return seg.Substring(0, seg.Length - 5);
            if (seg.EndsWith(".rho", StringComparison.OrdinalIgnoreCase)) return seg.Substring(0, seg.Length - 4);
            return seg;
        }

        /// <summary>경로 비교용: 소문자, '/' 통일, '_' 도 구분자로 취급해서 zeta_/kr 과 zeta/kr, zeta_kr 을 같게 봄</summary>
        private static string NormPath(string s)
        {
            s = s.ToLowerInvariant().Replace('\\', '/').Replace('_', '/');
            while (s.Contains("//")) s = s.Replace("//", "/");
            return s.Trim('/');
        }

        private static int FindSeg(string[] segs, string name)
        {
            for (int i = 0; i < segs.Length - 1; i++)
                if (string.Equals(segs[i].TrimEnd('_'), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private static void HandleRawFile(string root, string diskPath, Dictionary<string, string> paths, List<KeyValuePair<string, string>> docKeys, Dictionary<string, int> kartParamPri)
        {
            string rel = diskPath.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            string[] segs = rel.Split('/').Select(StripArchiveExt).ToArray();
            string file = segs[segs.Length - 1];

            // 카트 스펙: kart_/<이름>/param@<지역>.xml > param@<지역>.kml > param.xml (Data 폴더 방식과 같은 우선순위)
            int ki = FindSeg(segs, "kart");
            if (ki >= 0 && segs.Length - ki >= 3)
            {
                int pri = file.Equals($"param@{regionCode}.xml", StringComparison.OrdinalIgnoreCase) ? 0
                        : file.Equals($"param@{regionCode}.kml", StringComparison.OrdinalIgnoreCase) ? 1
                        : file.Equals("param.xml", StringComparison.OrdinalIgnoreCase) ? 2 : -1;
                if (pri < 0) return;
                string name = string.Join("/", segs, ki + 1, segs.Length - ki - 2);
                if (kartParamPri.TryGetValue(name, out int old) && old <= pri) return;
                Kart.kartSpec[name] = LoadXml(File.ReadAllBytes(diskPath));
                kartParamPri[name] = pri;
                rawKartDirs[name] = Path.GetDirectoryName(diskPath);
                return;
            }

            // 비행펫 스펙: flyingPet/<이름>/param@<지역>.bml
            int fi = FindSeg(segs, "flyingPet");
            if (fi >= 0 && segs.Length - fi >= 3 && file.Equals($"param@{regionCode}.bml", StringComparison.OrdinalIgnoreCase))
            {
                string name = string.Join("/", segs, fi + 1, segs.Length - fi - 2);
                if (!FlyingPet.flyingSpec.ContainsKey(name))
                    FlyingPet.flyingSpec.Add(name, ParseDoc(file, File.ReadAllBytes(diskPath)));
                rawPetDirs[name] = Path.GetDirectoryName(diskPath);
                return;
            }

            // 카트/펫 이름 테이블
            if (file.Equals("itemTable.kml", StringComparison.OrdinalIgnoreCase) || file.Equals($"itemTable@{regionCode}.xml", StringComparison.OrdinalIgnoreCase))
            {
                LoadItemTable(File.ReadAllBytes(diskPath));
                return;
            }

            // 상점 아이템 / 그 밖의 문서: 경로 끝부분이 일치하는 항목
            string n = NormPath(string.Join("/", segs));
            foreach (KeyValuePair<string, string> kv in docKeys)
            {
                string k = kv.Key;
                if (!n.EndsWith(k, StringComparison.Ordinal) || (n.Length != k.Length && n[n.Length - k.Length - 1] != '/')) continue;
                string key = kv.Value;
                LauncherSettings cfg = LauncherSettings.Current;
                bool isShop = key == "shopItem";
                if (!isShop && !cfg.LoadContentDocs) return;
                if (Docs.ContainsKey(key)) return;
                XmlDocument doc = ParseDoc(file, File.ReadAllBytes(diskPath));
                if (cfg.LoadContentDocs) Docs[key] = doc;
                if (isShop) LoadShopItems(doc);
                return;
            }
        }

        // ===================================================================
        //  폴더 방식: 주행 시작 때 이번에 쓸 카트/펫 스펙만 다시 읽기
        //  (게임을 켠 채로 DataRaw 의 param 파일을 고쳐도 다음 주행부터 바로 반영됨)
        // ===================================================================

        private static byte[] ReadAllBytesShared(string path)
        {
            // 편집기가 파일을 열어 둔 상태에서도 읽을 수 있게 공유 모드로 읽음
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>이름별로 기억해 둔 폴더를 돌려줌. 없으면(새로 추가된 카트 등) 폴더 안에서 같은 이름의 폴더를 찾아 봄</summary>
        private static string FindRawDir(Dictionary<string, string> known, string name, string parentSeg)
        {
            string dir;
            if (known.TryGetValue(name, out dir) && Directory.Exists(dir)) return dir;
            try
            {
                string leaf = name.Substring(name.LastIndexOf('/') + 1);
                string tail = Path.DirectorySeparatorChar + name.Replace('/', Path.DirectorySeparatorChar);
                foreach (string d in Directory.EnumerateDirectories(rawRoot, leaf, SearchOption.AllDirectories))
                {
                    if (!d.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) continue;
                    string parent = StripArchiveExt(Path.GetFileName(d.Substring(0, d.Length - tail.Length)));
                    if (!string.Equals(parent.TrimEnd('_'), parentSeg, StringComparison.OrdinalIgnoreCase)) continue;
                    known[name] = d;
                    return d;
                }
            }
            catch { }
            return null;
        }

        private static void LogReload(string kind, string name, string before, string after, string file)
        {
            if (before == null) Console.WriteLine("[Rho] 스펙 새로 읽음: {0} {1} ({2})", kind, name, file);
            else if (before != after) Console.WriteLine("[Rho] 스펙 변경 감지: {0} {1} — 새 값으로 적용 ({2})", kind, name, file);
            else Console.WriteLine("[Rho] 스펙 다시 읽음: {0} {1} (변경 없음)", kind, name);
        }

        private static void ReloadKartSpec(string name)
        {
            string dir = FindRawDir(rawKartDirs, name, "kart");
            if (dir == null) { Console.WriteLine("[Rho] 스펙 다시 읽기: 카트 폴더를 찾지 못해 기존 값을 써요: " + name); return; }
            string file = null;
            foreach (string cand in new[] { $"param@{regionCode}.xml", $"param@{regionCode}.kml", "param.xml" })
            {
                string p = Path.Combine(dir, cand);
                if (File.Exists(p)) { file = p; break; }
            }
            if (file == null) { Console.WriteLine("[Rho] 스펙 다시 읽기: param 파일이 없어 기존 값을 써요: " + dir); return; }
            XmlDocument doc = LoadXml(ReadAllBytesShared(file));
            XmlDocument old;
            string before = Kart.kartSpec.TryGetValue(name, out old) && old != null ? old.OuterXml : null;
            Kart.kartSpec[name] = doc;
            LogReload("카트", name, before, doc.OuterXml, Path.GetFileName(file));
        }

        private static void ReloadPetSpec(string name)
        {
            string dir = FindRawDir(rawPetDirs, name, "flyingPet");
            if (dir == null) { Console.WriteLine("[Rho] 스펙 다시 읽기: 펫 폴더를 찾지 못해 기존 값을 써요: " + name); return; }
            string file = Path.Combine(dir, $"param@{regionCode}.bml");
            if (!File.Exists(file)) { Console.WriteLine("[Rho] 스펙 다시 읽기: 펫 param 파일이 없어 기존 값을 써요: " + file); return; }
            XmlDocument doc = ParseDoc(file, ReadAllBytesShared(file));
            XmlDocument old;
            string before = FlyingPet.flyingSpec.TryGetValue(name, out old) && old != null ? old.OuterXml : null;
            FlyingPet.flyingSpec[name] = doc;
            LogReload("펫", name, before, doc.OuterXml, Path.GetFileName(file));
        }

        /// <summary>
        /// 주행 시작(PqKartSpec 등) 때 호출: 폴더 방식이면 이번에 쓸 카트/펫의 param 파일만 다시 읽어서 최신 값으로 적용.
        /// Data 폴더(aaa.pk) 방식이면 아무것도 하지 않음. 읽기에 실패하면 기존에 읽어 둔 값을 그대로 씀.
        /// </summary>
        public static void ReloadForRace(int kartId, int petId)
        {
            if (!FromFolder || !Loaded || string.IsNullOrEmpty(rawRoot)) return;
            try
            {
                KartTable table;
                if (kartId != 0 && Kart.kartName.TryGetValue(kartId, out table) && table != null)
                {
                    try { ReloadKartSpec(table.Name); }
                    catch (Exception ex) { Console.WriteLine("[Rho] 카트 스펙 다시 읽기 실패 (기존 값 사용): " + ex.Message); }
                }
                string pet;
                if (petId != 0 && FlyingPet.flyingName.TryGetValue(petId, out pet))
                {
                    try { ReloadPetSpec(pet); }
                    catch (Exception ex) { Console.WriteLine("[Rho] 펫 스펙 다시 읽기 실패 (기존 값 사용): " + ex.Message); }
                }
            }
            catch (Exception ex) { Console.WriteLine("[Rho] 스펙 다시 읽기 실패 (기존 값 사용): " + ex.Message); }
        }

        // ===================================================================
        //  읽기 방식 전환 (설정의 Data / DataRaw 슬라이더)
        //   - KartRider.xml 의 datapackOff 항목을 넣거나 빼서 게임 클라이언트가 읽는 방식도 같이 바꾸고,
        //   - 런처가 읽어 둔 스펙·아이템 데이터를 비우고 새 방식으로 다시 읽음
        // ===================================================================

        /// <summary>게임 폴더 (런처가 있는 폴더)</summary>
        public static string GameDir = "";

        /// <summary>지금 적용 중인 읽기 방식</summary>
        public static GameDataSource CurrentSource { get; set; }

        /// <summary>읽기 방식이 바뀌어 다시 읽기 시작할 때 (UI 스레드에서 발생)</summary>
        public static event Action<GameDataSource> SourceChanged;

        /// <summary>데이터를 읽을 때마다 올라가는 번호 (아이템 추가 창이 목록을 새로 만들 때 확인용)</summary>
        public static int LoadVersion = 0;

        /// <summary>
        /// KartRider.xml 이 없으면 기본 내용(UTF-16, 빈 config)으로 만듦. 그래야 datapackOff 설정을 넣고 뺄 수 있음.
        /// 새로 만들었으면 true.
        /// </summary>
        public static bool EnsureConfigFile(string gameDir)
        {
            string xmlPath = Path.Combine(gameDir, "KartRider.xml");
            try
            {
                if (File.Exists(xmlPath)) return false;
                string text = "<?xml version='1.0' encoding='UTF-16'?>\r\n<config>\r\n</config>";
                byte[] body = Encoding.Unicode.GetBytes(text);
                byte[] pre = Encoding.Unicode.GetPreamble();   // UTF-16 BOM (선언한 encoding 과 일치)
                var bytes = new byte[pre.Length + body.Length];
                Buffer.BlockCopy(pre, 0, bytes, 0, pre.Length);
                Buffer.BlockCopy(body, 0, bytes, pre.Length, body.Length);
                File.WriteAllBytes(xmlPath, bytes);
                Console.WriteLine("[런처] KartRider.xml 이 없어서 기본 파일을 만들었어요 (UTF-16)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[런처] KartRider.xml 을 만들지 못했어요: " + ex.Message);
                return false;
            }
        }

        /// <summary>KartRider.xml 에 (주석이 아닌) datapackOff 항목이 있는지</summary>
        public static bool IsRawModeEnabled(string gameDir)
        {
            string note;
            return ReadDatapackOffPath(Path.Combine(gameDir, "KartRider.xml"), out note) != null;
        }

        /// <summary>KartRider.xml 에 적힌 datapackOff 의 path (없으면 null)</summary>
        public static string GetDatapackOffPath(string gameDir)
        {
            string note;
            return ReadDatapackOffPath(Path.Combine(gameDir, "KartRider.xml"), out note);
        }

        /// <summary>설정에 기억해 둔 datapackOff 항목의 path (없으면 "DataRaw")</summary>
        public static string RememberedPath()
        {
            var m = System.Text.RegularExpressions.Regex.Match(LauncherSettings.Current.DatapackOffElement ?? "", @"\bpath\s*=\s*(['""])(.*?)\1");
            return m.Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : "DataRaw";
        }

        /// <summary>항목 문자열의 path 속성 값을 바꿈 (path 가 없으면 추가)</summary>
        private static string WithPathAttr(string element, string p)
        {
            string q = p.Contains("'") ? "\"" : "'";
            string esc = p.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            if (q == "\"") esc = esc.Replace("\"", "&quot;");
            string attr = "path=" + q + esc + q;
            var rx = new System.Text.RegularExpressions.Regex(@"\bpath\s*=\s*(['""]).*?\1", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (rx.IsMatch(element)) return rx.Replace(element, m => attr, 1);
            int sp = element.IndexOf("<datapackOff", StringComparison.OrdinalIgnoreCase);
            return sp >= 0 ? element.Insert(sp + "<datapackOff".Length, " " + attr) : element;
        }

        /// <summary>
        /// KartRider.xml 의 datapackOff 를 켜고(path 지정) 끔.
        ///  - 켜기: 항목이 있으면 path 만 바꾸고, 없으면 새로 추가 (설정에 기억해 둔 항목을 바탕으로)
        ///  - 끄기: 항목을 제거하고 설정에 기억해 둠 (다시 켤 때 같은 내용으로 복원)
        /// 파일의 다른 부분과 인코딩은 그대로 두고, 처음 바꿀 때 KartRider.xml.launcher-bak 으로 백업.
        /// </summary>
        public static bool SetDatapackOff(string gameDir, bool enable, string pathValue, out string message)
        {
            message = null;
            string xmlPath = Path.Combine(gameDir, "KartRider.xml");
            try
            {
                if (!File.Exists(xmlPath)) { message = "KartRider.xml 이 없어서 바꿀 수 없어요"; return false; }
                byte[] raw = File.ReadAllBytes(xmlPath);
                bool le = raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE;
                bool be = raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF;
                // UTF-16 이 아니면 Latin1 로 1:1 변환해서 편집 (UTF-8 한글 등 다른 바이트가 그대로 보존됨)
                Encoding enc = le ? Encoding.Unicode : be ? Encoding.BigEndianUnicode : Encoding.Latin1;
                int skip = (le || be) ? 2 : 0;
                string text = enc.GetString(raw, skip, raw.Length - skip);

                var comments = System.Text.RegularExpressions.Regex.Matches(text, "<!--.*?-->", System.Text.RegularExpressions.RegexOptions.Singleline);
                System.Text.RegularExpressions.Match found = null;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"<datapackOff\b[^>]*>(?:\s*</datapackOff\s*>)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    bool inComment = false;
                    foreach (System.Text.RegularExpressions.Match c in comments)
                        if (m.Index >= c.Index && m.Index < c.Index + c.Length) { inComment = true; break; }
                    if (!inComment) { found = m; break; }
                }

                string newText;
                if (enable)
                {
                    string p = string.IsNullOrWhiteSpace(pathValue) ? RememberedPath() : pathValue.Trim();
                    if (found != null)
                    {
                        string updated = WithPathAttr(found.Value, p);
                        if (updated == found.Value) { message = "KartRider.xml 의 datapackOff 가 이미 같은 폴더예요"; return true; }
                        newText = text.Remove(found.Index, found.Length).Insert(found.Index, updated);
                        message = "KartRider.xml 의 datapackOff path 를 '" + p + "' 로 바꿨어요";
                    }
                    else
                    {
                        string template = LauncherSettings.Current.DatapackOffElement;
                        if (string.IsNullOrWhiteSpace(template)) template = "<datapackOff path='DataRaw' checkRfl='false'/>";
                        string element = WithPathAttr(template, p);
                        int close = text.LastIndexOf("</", StringComparison.Ordinal);
                        if (close < 0) { message = "KartRider.xml 형식을 알 수 없어 항목을 추가하지 못했어요"; return false; }
                        string nl = text.Contains("\r\n") ? "\r\n" : "\n";
                        int lineStart = text.LastIndexOf('\n', close) + 1;
                        bool ownLine = text.Substring(lineStart, close - lineStart).Trim().Length == 0;
                        if (ownLine)
                        {
                            var im = System.Text.RegularExpressions.Regex.Match(text, @"\n([ \t]+)<(?![/?!])");
                            string indent = im.Success ? im.Groups[1].Value : "\t";
                            newText = text.Insert(lineStart, indent + element + nl);
                        }
                        else newText = text.Insert(close, element);
                        message = "KartRider.xml 에 " + element + " 를 추가했어요";
                    }
                }
                else
                {
                    if (found == null) { message = "KartRider.xml 에 datapackOff 가 없어요 (이미 꺼짐)"; return true; }
                    LauncherSettings.Current.DatapackOffElement = found.Value.Trim();
                    LauncherSettings.Save();
                    int s = found.Index, e = found.Index + found.Length;
                    int lineStart = s == 0 ? 0 : text.LastIndexOf('\n', s - 1) + 1;
                    int lineEnd = text.IndexOf('\n', e);
                    lineEnd = lineEnd < 0 ? text.Length : lineEnd + 1;
                    bool wholeLine = text.Substring(lineStart, s - lineStart).Trim().Length == 0 && text.Substring(e, lineEnd - e).Trim().Length == 0;
                    newText = wholeLine ? text.Remove(lineStart, lineEnd - lineStart) : text.Remove(s, e - s);
                    message = "KartRider.xml 에서 datapackOff 항목을 뺐어요";
                }

                string bak = xmlPath + ".launcher-bak";
                if (!File.Exists(bak)) File.Copy(xmlPath, bak);
                byte[] body = enc.GetBytes(newText);
                byte[] pre = skip > 0 ? enc.GetPreamble() : new byte[0];
                var outBytes = new byte[pre.Length + body.Length];
                Buffer.BlockCopy(pre, 0, outBytes, 0, pre.Length);
                Buffer.BlockCopy(body, 0, outBytes, pre.Length, body.Length);
                File.WriteAllBytes(xmlPath, outBytes);
                return true;
            }
            catch (Exception ex)
            {
                message = "KartRider.xml 을 바꾸지 못했어요: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 읽기 방식을 바꾼 뒤 호출: 지금까지 읽은 카트·펫·아이템 데이터를 비우고 새 방식으로 다시 읽음.
        /// 읽는 중이면 false (끝난 뒤에 다시 시도).
        /// </summary>
        public static bool Reload(string gameDir)
        {
            if (Status == "읽는 중") return false;
            GameDataSource src = ResolveSource(gameDir);
            Loaded = false;   // 다시 읽는 동안 주행 시작은 기본 성능으로 처리됨 (반쯤 읽힌 데이터를 쓰지 않도록)
            Kart.kartName.Clear();
            Kart.kartSpec.Clear();
            FlyingPet.flyingName.Clear();
            FlyingPet.flyingSpec.Clear();
            NewRider.items.Clear();
            ItemTableTags.Clear();
            Docs.Clear();
            rawKartDirs.Clear();
            rawPetDirs.Clear();
            LoadVersion++;
            Console.WriteLine("[Rho] 읽기 방식이 바뀌어 게임 데이터를 다시 읽어요");
            CurrentSource = src;
            Action<GameDataSource> h = SourceChanged;
            if (h != null) h(src);
            StartLoad(src);
            return true;
        }

        public static void Dump(string input)
        {
            PackFolderManager mgr = null;
            try
            {
                Console.WriteLine("[Rho] 게임 데이터 읽는 중: " + input);
                regionCode = GetRegionCode(input);
                Console.WriteLine("[Rho] regionCode = " + regionCode);

                mgr = new PackFolderManager();
                mgr.OpenDataFolder(input);
                var paths = BuildDocPaths(regionCode);
                var kartParamPri = new Dictionary<string, int>();
                var queue = new Queue<PackFolderInfo>();
                queue.Enqueue(mgr.GetRootFolder());
                int fileCount = 0, failCount = 0;
                while (queue.Count > 0)
                {
                    PackFolderInfo folder = queue.Dequeue();
                    foreach (PackFileInfo f in folder.GetFilesInfo())
                    {
                        fileCount++;
                        try { HandleFile(f, paths, kartParamPri); }
                        catch (Exception ex)
                        {
                            failCount++;
                            Console.WriteLine($"[Rho] 건너뜀 {f.FullName}: {ex.Message}");
                        }
                    }
                    foreach (PackFolderInfo sub in folder.GetFoldersInfo()) queue.Enqueue(sub);
                }

                int itemCount = NewRider.items.Values.Sum(v => v.Count);
                Console.WriteLine($"[Rho] 파일 {fileCount}개 검사, 실패 {failCount}개");
                Console.WriteLine($"[Rho] 카트 이름 {Kart.kartName.Count}개, 카트 스펙 {Kart.kartSpec.Count}개, 펫 이름 {FlyingPet.flyingName.Count}개, 펫 스펙 {FlyingPet.flyingSpec.Count}개, 상점 아이템 {itemCount}개");
                if (LauncherSettings.Current.LoadContentDocs)
                {
                    var missing = paths.Values.Distinct().Where(k => !Docs.ContainsKey(k)).ToList();
                    Console.WriteLine("[Rho] 읽은 문서: " + string.Join(", ", Docs.Keys));
                    if (missing.Count > 0) Console.WriteLine("[Rho] 못 찾은 문서(구버전에 없을 수 있음): " + string.Join(", ", missing));
                }
                Loaded = true;
                Status = "완료";
                Console.WriteLine("[Rho] 로드 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Rho] 로드 실패 -> 카트·펫 스펙은 기본 성능으로 처리돼요: " + ex);
                Status = "실패";
            }
            finally
            {
                // 게임이 같은 .rho 파일을 열 수 있도록, 읽기가 끝나면 열어 둔 파일을 모두 닫음
                try
                {
                    if (mgr != null)
                    {
                        mgr.Reset();
                        Console.WriteLine("[Rho] 게임 데이터 파일 핸들 해제 완료");
                    }
                }
                catch (Exception ex2) { Console.WriteLine("[Rho] 파일 핸들 해제 중 오류: " + ex2.Message); }
            }
        }

        private static void HandleFile(PackFileInfo f, Dictionary<string, string> paths, Dictionary<string, int> kartParamPri)
        {
            string full = ReplacePath(f.FullName);

            // 카트 스펙: kart_/<이름>/param@<지역>.xml > param@<지역>.kml > param.xml (V2와 같은 우선순위)
            if (full.StartsWith("kart_/"))
            {
                int pri = full.EndsWith($"/param@{regionCode}.xml") ? 0
                        : full.EndsWith($"/param@{regionCode}.kml") ? 1
                        : full.EndsWith("/param.xml") ? 2 : -1;
                if (pri < 0) return;
                string name = full.Substring(6, full.LastIndexOf('/') - 6);
                if (kartParamPri.TryGetValue(name, out int old) && old <= pri) return;
                Kart.kartSpec[name] = LoadXml(f.GetData());
                kartParamPri[name] = pri;
                return;
            }

            // 비행펫 스펙: flyingPet/<이름>/param@<지역>.bml
            if (full.Contains("flyingPet/") && full.EndsWith($"param@{regionCode}.bml"))
            {
                int s = full.IndexOf("flyingPet/") + 10;
                string name = full.Substring(s, full.LastIndexOf('/') - s);
                if (!FlyingPet.flyingSpec.ContainsKey(name))
                    FlyingPet.flyingSpec.Add(name, ParseDoc(full, f.GetData()));
                return;
            }

            // 카트/펫 이름 테이블 (itemTable.kml 과 itemTable@<지역>.xml 모두 읽어 합침)
            if (full == "etc_/itemTable.kml" || full == $"etc_/itemTable@{regionCode}.xml")
            {
                LoadItemTable(f.GetData());
                return;
            }

            // 그 밖의 문서(퀘스트/상점/이벤트/채널/트랙 등): 원본 그대로 보관
            if (paths.TryGetValue(full, out string key))
            {
                LauncherSettings cfg = LauncherSettings.Current;
                bool isShop = key == "shopItem";
                if (!isShop && !cfg.LoadContentDocs) return;   // 상점 아이템은 항상 읽고, 나머지 문서는 (실험) 설정일 때만
                if (Docs.ContainsKey(key)) return;
                XmlDocument doc = ParseDoc(full, f.GetData());
                if (cfg.LoadContentDocs) Docs[key] = doc;
                if (isShop) LoadShopItems(doc);
            }
        }

        private static void LoadItemTable(byte[] data)
        {
            XDocument doc;
            using (var ms = new MemoryStream(data)) doc = XDocument.Load(ms);
            if (doc.Root != null)
            {
                foreach (XElement el in doc.Root.Elements())
                {
                    XAttribute ida = el.Attribute("id");
                    if (ida == null || !int.TryParse(ida.Value, out int tid)) continue;
                    string tag = el.Name.LocalName;
                    if (!ItemTableTags.TryGetValue(tag, out HashSet<int> set)) { set = new HashSet<int>(); ItemTableTags[tag] = set; }
                    set.Add(tid);
                }
            }
            foreach (var k in doc.Descendants("kart").Where(x => x.Attribute("name") != null && x.Attribute("id") != null))
            {
                if (!int.TryParse(k.Attribute("id").Value, out int id)) continue;
                string name = k.Attribute("name").Value;
                byte.TryParse(k.Attribute("grade")?.Value ?? "0", out byte grade);
                List<short> enchant = (k.Attribute("defaultEnchant")?.Value ?? "")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => short.TryParse(s.Trim(), out var v) ? (short?)v : null)
                    .Where(v => v.HasValue).Select(v => v.Value).ToList();
                if (Kart.kartName.TryGetValue(id, out KartTable t))
                {
                    if (t.grade == 0 && grade != 0) t.grade = grade;
                    if (t.defaultEnchant.Count < 1 && enchant.Count > 0) t.defaultEnchant = enchant;
                }
                else Kart.kartName.Add(id, new KartTable { Name = name, grade = grade, defaultEnchant = enchant });
            }
            foreach (var p in doc.Descendants("flyingPet").Where(x => x.Attribute("name") != null && x.Attribute("id") != null))
            {
                if (int.TryParse(p.Attribute("id").Value, out int id)) FlyingPet.flyingName[id] = p.Attribute("name").Value;
            }
        }

        /// <summary>상점 item.kml -> NewRider.items[itemCatId][itemId] = itemName (V2와 동일)</summary>
        private static void LoadShopItems(XmlDocument doc)
        {
            foreach (XmlNode xn in doc.GetElementsByTagName("item"))
            {
                var xe = (XmlElement)xn;
                if (!ushort.TryParse(xe.GetAttribute("itemCatId"), out ushort cat)) continue;
                if (!ushort.TryParse(xe.GetAttribute("itemId"), out ushort id)) continue;
                if (!NewRider.items.ContainsKey(cat)) NewRider.items[cat] = new Dictionary<ushort, string>();
                NewRider.items[cat][id] = xe.GetAttribute("itemName");
            }
        }

        /// <summary>.bml(바이너리 XML)이면 변환 후, 아니면 그대로 XmlDocument 로 읽음</summary>
        private static XmlDocument ParseDoc(string path, byte[] data)
        {
            if (path.EndsWith(".bml", StringComparison.OrdinalIgnoreCase)) data = BmlToXml(path, data);
            try
            {
                var d = new XmlDocument();
                using (var ms = new MemoryStream(data)) d.Load(ms);
                return d;
            }
            catch { return LoadXml(data); }
        }

        /// <summary>
        /// 게임 XML 읽기.
        /// 1) V2/P3563 방식 그대로 (대부분의 파일은 여기서 성공)
        /// 2) 실패하면 인코딩 자동 판별 + NUL/중복 선언 제거 후 "첫 번째 루트 요소"만 읽음
        ///    (파일 뒤에 NUL 패딩이나 UTF-16 조각이 붙어 있어도 앞부분 XML만 사용)
        /// </summary>
        private static XmlDocument LoadXml(byte[] raw)
        {
            Exception last = null;
            try { return LoadXmlLegacy(raw); }
            catch (Exception ex) { last = ex; }
            foreach (string text in DecodeCandidates(raw))
            {
                try { return ReadFirstElement(text); }
                catch (Exception ex) { last = ex; }
            }
            throw last ?? new InvalidDataException("XML 디코딩 실패");
        }

        private static XmlDocument LoadXmlLegacy(byte[] raw)
        {
            byte[] d = ReplaceBytes(raw);
            if (d.Length > 6 && d[2] == 13 && d[3] == 0 && d[4] == 10 && d[5] == 0)
            {
                var nb = new byte[d.Length - 4];
                nb[0] = 255; nb[1] = 254;
                Array.Copy(d, 6, nb, 2, d.Length - 6);
                d = nb;
            }
            var doc = new XmlDocument();
            using (var ms = new MemoryStream(d)) doc.Load(ms);
            return doc;
        }

        private static XmlDocument ReadFirstElement(string text)
        {
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                CheckCharacters = false,
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true
            };
            using (var xr = XmlReader.Create(new StringReader(text), settings))
            {
                while (xr.Read())
                {
                    if (xr.NodeType == XmlNodeType.Element)
                    {
                        var doc = new XmlDocument();
                        doc.AppendChild(doc.ReadNode(xr));
                        return doc;
                    }
                }
            }
            throw new InvalidDataException("루트 요소를 찾지 못함");
        }

        private static IEnumerable<string> DecodeCandidates(byte[] d)
        {
            var list = new List<KeyValuePair<Encoding, int>>();
            if (d.Length >= 2 && d[0] == 0xFF && d[1] == 0xFE) list.Add(new KeyValuePair<Encoding, int>(Encoding.Unicode, 2));
            else if (d.Length >= 2 && d[0] == 0xFE && d[1] == 0xFF) list.Add(new KeyValuePair<Encoding, int>(Encoding.BigEndianUnicode, 2));
            else if (d.Length >= 3 && d[0] == 0xEF && d[1] == 0xBB && d[2] == 0xBF) list.Add(new KeyValuePair<Encoding, int>(Encoding.UTF8, 3));
            else
            {
                int n = Math.Min(d.Length, 200), zeros = 0;
                for (int i = 1; i < n; i += 2) if (d[i] == 0) zeros++;
                if (zeros > n / 8)
                {
                    list.Add(new KeyValuePair<Encoding, int>(Encoding.Unicode, 0));
                    list.Add(new KeyValuePair<Encoding, int>(Encoding.UTF8, 0));
                }
                else
                {
                    list.Add(new KeyValuePair<Encoding, int>(Encoding.UTF8, 0));
                    list.Add(new KeyValuePair<Encoding, int>(Encoding.Unicode, 0));
                }
            }
            // 판별이 틀렸을 때를 대비한 보조 후보
            list.Add(new KeyValuePair<Encoding, int>(Encoding.UTF8, 0));
            list.Add(new KeyValuePair<Encoding, int>(Encoding.Unicode, 0));
            foreach (var e in list)
                yield return CleanXmlText(e.Key.GetString(d, e.Value, d.Length - e.Value));
        }

        private static string CleanXmlText(string s)
        {
            s = s.Replace("\0", "").Replace("\uFEFF", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"<\?xml[^>]*\?>", "");
            return s.Trim();
        }

        // ===== 아래는 P3563 Rho.cs 에서 그대로 가져온 헬퍼 =====
        private static byte[] ReplaceBytes(byte[] data)
        {
            byte[] oldBytes = new byte[] {
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00,
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00
        };

            byte[] newBytes = new byte[] {
            0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00, 0x20, 0x00,
            0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x73, 0x00, 0x69, 0x00, 0x6F, 0x00, 0x6E, 0x00,
            0x3D, 0x00, 0x27, 0x00, 0x31, 0x00, 0x2E, 0x00, 0x30, 0x00, 0x27, 0x00, 0x20, 0x00,
            0x65, 0x00, 0x6E, 0x00, 0x63, 0x00, 0x6F, 0x00, 0x64, 0x00, 0x69, 0x00, 0x6E, 0x00,
            0x67, 0x00, 0x3D, 0x00, 0x27, 0x00, 0x55, 0x00, 0x54, 0x00, 0x46, 0x00, 0x2D, 0x00,
            0x31, 0x00, 0x36, 0x00, 0x27, 0x00, 0x3F, 0x00, 0x3E, 0x00, 0x0D, 0x00, 0x0A, 0x00
        };
            int oldLength = oldBytes.Length;
            int newLength = newBytes.Length;
            int dataLength = data.Length;

            byte[] result = new byte[dataLength];
            int resultIndex = 0;
            int i = 0;

            while (i < dataLength)
            {
                bool found = true;
                for (int j = 0; j < oldLength; j++)
                {
                    if (i + j >= dataLength || data[i + j] != oldBytes[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    for (int k = 0; k < newLength; k++)
                    {
                        result[resultIndex++] = newBytes[k];
                    }
                    i += oldLength;
                }
                else
                {
                    result[resultIndex++] = data[i++];
                }
            }

            Array.Resize(ref result, resultIndex);
            return result;
        }

        private static string ReplacePath(string file)
        {
            return file.IndexOf(".rho") > -1 ? file.Substring(0, file.IndexOf(".rho")).Replace("_", "/") + file.Substring(file.IndexOf(".rho") + 4) : file;
        }

        private static byte[] BmlToXml(string path, byte[] bmlData)
        {
            if (Path.GetExtension(path).ToLower() == ".bml")
            {
                BinaryXmlDocument bxd = new BinaryXmlDocument();
                bxd.Read(Encoding.GetEncoding("UTF-16"), bmlData);
                string output_bml = bxd.RootTag.ToString();
                byte[] output_data = Encoding.GetEncoding("UTF-16").GetBytes(output_bml);
                return output_data;
            }
            else
            {
                return bmlData;
            }
        }

        public static BinaryXmlTag GetAAATag(string input)
        {
            // Read the current aaa.pk content
            byte[] aaaPkData;
            try
            {
                using (FileStream fileStream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BinaryReader br = new BinaryReader(fileStream);
                    int dataLen = br.ReadInt32();
                    aaaPkData = br.ReadKRData(dataLen);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading aaa.pk: {ex.Message}");
                // If reading fails, return null
                return null;
            }

            BinaryXmlDocument bxmlDoc = new BinaryXmlDocument();
            bxmlDoc.Read(Encoding.GetEncoding("UTF-16"), aaaPkData);
            BinaryXmlTag rootTag = bxmlDoc.RootTag;
            return rootTag;
        }

        /// <summary>aaa.pk 의 zeta 폴더 구조에서 국가 리전(kr/cn/tw 등 2글자)을 찾음. 알 수 없으면 null (화면 표시용)</summary>
        public static string DetectRegion(string aaaPkPath)
        {
            try
            {
                BinaryXmlTag rootTag = GetAAATag(aaaPkPath);
                if (rootTag == null) return null;
                foreach (BinaryXmlTag zeta in rootTag.Children)
                {
                    if (zeta.Name != "PackFolder" || zeta.GetAttribute("name") != "zeta") continue;
                    foreach (BinaryXmlTag sub in zeta.Children)
                    {
                        if (sub.Name != "PackFolder") continue;
                        string name = sub.GetAttribute("name");
                        if (!string.IsNullOrEmpty(name) && name.Length == 2 && char.IsLetter(name[0]) && char.IsLetter(name[1]))
                            return name.ToLowerInvariant();
                    }
                }
            }
            catch { }
            return null;
        }

        public static string GetRegionCode(string input)
        {
            // Extract regionCode from the XML
            string regionCode = "cn"; // Default to CN

            BinaryXmlTag rootTag = GetAAATag(input);
            if (rootTag == null)
            {
                return regionCode;
            }

            try
            {
                // Try to find the region code by looking at zeta folders
                BinaryXmlTag zetaFolder = null;
                foreach (BinaryXmlTag subtag in rootTag.Children)
                {
                    if (subtag.Name == "PackFolder" && subtag.GetAttribute("name") == "zeta")
                    {
                        zetaFolder = subtag;
                        break;
                    }
                }

                if (zetaFolder != null)
                {
                    foreach (BinaryXmlTag subtag in zetaFolder.Children)
                    {
                        if (subtag.Name == "PackFolder")
                        {
                            string folderName = subtag.GetAttribute("name");
                            if (folderName == "kr")
                            {
                                regionCode = "kr";
                                break;
                            }
                            else if (folderName == "cn")
                            {
                                regionCode = "cn";
                                break;
                            }
                            else if (folderName == "tw")
                            {
                                regionCode = "tw";
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting regionCode: {ex.Message}");
            }
            return regionCode;
        }
    }
}
