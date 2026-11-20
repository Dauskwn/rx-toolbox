#if ADMIN_EDITION
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

public sealed class PinyinTitle
{
    public static readonly PinyinTitle Empty = new PinyinTitle(new string[0][], "");
    public readonly string[][] Units;
    public readonly string Initials;

    public PinyinTitle(string[][] units, string initials)
    {
        Units = units ?? new string[0][];
        Initials = initials ?? "";
    }
}

/// <summary>
/// 纯 C# 零外部依赖智能拼音引擎：支持全拼、首字母、全拼+首字母混拼（音节边界对齐）、
/// zh/ch/sh 双字母声母、常见多音字、GBK 二级/繁体常用游戏字及 Windows zh-CN 兜底。
/// </summary>
public static class PinyinHelper
{
    // 已全量审计 GB2312-80 一级字区（3,755 字）的 397 个音节精确边界字表
    const string GbkSyllableTable =
        "啊:a|埃:ai|鞍:an|肮:ang|凹:ao|芭:ba|白:bai|斑:ban|邦:bang|苞:bao|杯:bei|奔:ben|崩:beng|逼:bi|鞭:bian|标:biao|鳖:bie|彬:bin|兵:bing|玻:bo|捕:bu|" +
        "擦:ca|猜:cai|餐:can|苍:cang|操:cao|厕:ce|层:ceng|插:cha|拆:chai|搀:chan|昌:chang|超:chao|车:che|郴:chen|撑:cheng|吃:chi|充:chong|抽:chou|初:chu|揣:chuai|川:chuan|疮:chuang|吹:chui|春:chun|戳:chuo|疵:ci|聪:cong|凑:cou|粗:cu|蹿:cuan|摧:cui|村:cun|磋:cuo|" +
        "搭:da|呆:dai|耽:dan|当:dang|刀:dao|德:de|蹬:deng|堤:di|颠:dian|碉:diao|跌:die|丁:ding|丢:diu|东:dong|兜:dou|都:du|端:duan|堆:dui|墩:dun|掇:duo|" +
        "蛾:e|恩:en|而:er|" +
        "发:fa|藩:fan|坊:fang|菲:fei|芬:fen|丰:feng|佛:fo|否:fou|夫:fu|" +
        "噶:ga|该:gai|干:gan|冈:gang|篙:gao|哥:ge|给:gei|根:gen|耕:geng|工:gong|钩:gou|辜:gu|刮:gua|乖:guai|棺:guan|光:guang|瑰:gui|辊:gun|锅:guo|" +
        "哈:ha|骸:hai|酣:han|夯:hang|壕:hao|呵:he|嘿:hei|痕:hen|哼:heng|轰:hong|喉:hou|呼:hu|花:hua|槐:huai|欢:huan|荒:huang|灰:hui|荤:hun|豁:huo|" +
        "击:ji|嘉:jia|歼:jian|僵:jiang|蕉:jiao|揭:jie|巾:jin|荆:jing|炯:jiong|揪:jiu|鞠:ju|捐:juan|撅:jue|均:jun|" +
        "喀:ka|开:kai|刊:kan|康:kang|考:kao|坷:ke|肯:ken|坑:keng|空:kong|抠:kou|枯:ku|夸:kua|块:kuai|宽:kuan|匡:kuang|亏:kui|坤:kun|括:kuo|" +
        "垃:la|莱:lai|蓝:lan|琅:lang|捞:lao|勒:le|雷:lei|棱:leng|厘:li|俩:lia|联:lian|粮:liang|撩:liao|列:lie|琳:lin|拎:ling|溜:liu|龙:long|楼:lou|芦:lu|驴:lv|峦:luan|掠:lue|抡:lun|萝:luo|" +
        "妈:ma|埋:mai|瞒:man|芒:mang|猫:mao|么:me|玫:mei|门:men|萌:meng|眯:mi|棉:mian|苗:miao|蔑:mie|民:min|明:ming|谬:miu|摸:mo|谋:mou|拇:mu|" +
        "拿:na|氖:nai|南:nan|囊:nang|挠:nao|呢:ne|馁:nei|嫩:nen|能:neng|妮:ni|蔫:nian|娘:niang|鸟:niao|捏:nie|您:nin|柠:ning|牛:niu|脓:nong|奴:nu|女:nv|暖:nuan|虐:nue|挪:nuo|" +
        "哦:o|欧:ou|" +
        "啪:pa|拍:pai|攀:pan|乓:pang|抛:pao|呸:pei|喷:pen|砰:peng|坯:pi|篇:pian|飘:piao|撇:pie|拼:pin|乒:ping|坡:po|剖:pou|扑:pu|" +
        "期:qi|掐:qia|牵:qian|枪:qiang|橇:qiao|切:qie|钦:qin|青:qing|琼:qiong|秋:qiu|趋:qu|圈:quan|缺:que|裙:qun|" +
        "然:ran|瓤:rang|饶:rao|惹:re|壬:ren|扔:reng|日:ri|戎:rong|揉:rou|茹:ru|软:ruan|蕊:rui|闰:run|若:ruo|" +
        "撒:sa|腮:sai|三:san|桑:sang|搔:sao|瑟:se|森:sen|僧:seng|莎:sha|筛:shai|珊:shan|墒:shang|梢:shao|奢:she|砷:shen|声:sheng|师:shi|收:shou|蔬:shu|刷:shua|摔:shuai|栓:shuan|霜:shuang|谁:shui|吮:shun|说:shuo|斯:si|松:song|搜:sou|苏:su|酸:suan|虽:sui|孙:sun|蓑:suo|" +
        "塌:ta|胎:tai|坍:tan|汤:tang|掏:tao|特:te|藤:teng|梯:ti|天:tian|挑:tiao|贴:tie|厅:ting|通:tong|偷:tou|凸:tu|湍:tuan|推:tui|吞:tun|拖:tuo|" +
        "挖:wa|歪:wai|豌:wan|汪:wang|威:wei|瘟:wen|嗡:weng|挝:wo|巫:wu|" +
        "昔:xi|瞎:xia|掀:xian|相:xiang|萧:xiao|楔:xie|薪:xin|星:xing|兄:xiong|休:xiu|墟:xu|轩:xuan|靴:xue|勋:xun|" +
        "压:ya|阉:yan|殃:yang|邀:yao|椰:ye|一:yi|茵:yin|英:ying|哟:yo|拥:yong|幽:you|迂:yu|鸳:yuan|曰:yue|耘:yun|" +
        "匝:za|栽:zai|咱:zan|赃:zang|遭:zao|责:ze|贼:zei|怎:zen|增:zeng|扎:zha|摘:zhai|瞻:zhan|樟:zhang|招:zhao|遮:zhe|珍:zhen|蒸:zheng|芝:zhi|中:zhong|舟:zhou|珠:zhu|抓:zhua|拽:zhuai|专:zhuan|桩:zhuang|椎:zhui|谆:zhun|捉:zhuo|兹:zi|鬃:zong|邹:zou|租:zu|钻:zuan|嘴:zui|尊:zun|昨:zuo";

    // Layer 1：显式多音字 + GB2312 异常乱序字 + 游戏常用二级/繁体字（首个读音作为默认首字母）
    const string SpecialPinyinTable =
        "剥:bo,bao|辟:pi,bi|泊:bo,po|长:chang,zhang|匙:shi,chi|地:di,de|都:du,dou|脯:fu,pu|蛤:ge,ha|还:hai,huan|" +
        "槛:kan,jian|嚼:jiao,jue|桔:ju,jie|藉:ji,jie|咯:ge,ka,lo|咳:ke,hai|了:le,liao|拎:lin,ling|什:shen,shi|谁:shui,shei|" +
        "伺:ci,si|似:si,shi|拓:tuo,ta|厦:sha,xia|削:xiao,xue|畜:chu,xu|咽:yan,ye|吁:xu,yu|钥:yao,yue|曾:ceng,zeng|" +
        "轧:ya,zha|咋:za,zha|帧:zhen|爪:zhua,zhao|琢:zuo,zhuo|着:zhao,zhe,zhuo|仔:zai,zi|柞:zha,zuo|" +
        "重:zhong,chong|弹:dan,tan|血:xue,xie|行:xing,hang|调:tiao,diao|差:cha,chai|传:chuan,zhuan|藏:cang,zang|" +
        "降:jiang,xiang|解:jie,xie|觉:jue,jiao|卡:ka,qia|乐:le,yue|露:lu,lou|落:luo,lao|秘:mi,bi|模:mo,mu|" +
        "炮:pao,bao|屏:ping,bing|奇:qi,ji|强:qiang,jiang|色:se,shai|折:zhe,she|省:sheng,xing|率:lv,lu,shuai|" +
        "宿:su,xiu|提:ti,di|校:xiao,jiao|系:xi,ji|吓:xia,he|单:dan,shan|得:de,dei|给:gei,ji|和:he,huo|" +
        "会:hui,kuai|角:jiao,jue|劲:jin,jing|埋:mai,man|没:mei,mo|哪:na,nei|迫:po,pai|圈:quan,juan|塞:sai,se|" +
        "识:shi,zhi|说:shuo,shui|朝:chao,zhao|称:cheng,chen|乘:cheng,sheng|盛:sheng,cheng|度:du,duo|抹:mo,ma|嗯:en,ng|" +
        "沌:dun|霰:xian|铐:kao|弩:nu|狩:shou|隼:sun|冥:ming|鸢:yuan|傳:chuan,zhuan|刎:wen|谟:mo|牒:die|" +
        "祀:si|臾:yu|莓:mei|槌:chui|瑕:xia|跤:jiao|嘟:du|哒:da|馨:xin|匕:bi|铠:kai|弑:shi|魅:mei|魇:yan|" +
        "噬:shi|骸:hai|傀:kui|儡:lei|獠:liao|烬:jin|湮:yan|凛:lin|穹:qiong|琥:hu|珀:po|榴:liu|戟:ji|镰:lian";

    struct ZhAnchor
    {
        public string Anchor;
        public string Pinyin;
    }

    static readonly object syncLock = new object();
    static bool coreInited = false;
    static Encoding gbkEnc;
    static int[] gbkCodes;
    static string[] gbkPinyins;
    static Dictionary<char, string[]> specialMap;
    static readonly Dictionary<char, string[]> charCache = new Dictionary<char, string[]>();

    static bool zhInited = false;
    static CompareInfo zhCompare;
    static ZhAnchor[] zhAnchors;

    static void AddExpandedSyllables(List<string> dst, string py)
    {
        if (string.IsNullOrEmpty(py)) return;
        if (!dst.Contains(py)) dst.Add(py);
        if (py == "lv" && !dst.Contains("lu")) dst.Add("lu");
        else if (py == "lve" && !dst.Contains("lue")) dst.Add("lue");
        else if (py == "lue" && !dst.Contains("lve")) dst.Add("lve");
        else if (py == "nv" && !dst.Contains("nu")) dst.Add("nu");
        else if (py == "nve" && !dst.Contains("nue")) dst.Add("nue");
        else if (py == "nue" && !dst.Contains("nve")) dst.Add("nve");
    }

    static string[] ExpandSyllables(string[] raw)
    {
        var list = new List<string>(raw.Length + 2);
        for (int i = 0; i < raw.Length; i++)
            AddExpandedSyllables(list, raw[i]);
        return list.ToArray();
    }

    static void EnsureCoreInit()
    {
        if (coreInited) return;
        lock (syncLock)
        {
            if (coreInited) return;
            try { gbkEnc = Encoding.GetEncoding(936); }
            catch { gbkEnc = null; }

            var sm = new Dictionary<char, string[]>();
            string[] sParts = SpecialPinyinTable.Split('|');
            for (int i = 0; i < sParts.Length; i++)
            {
                int colon = sParts[i].IndexOf(':');
                if (colon <= 0) continue;
                char ch = sParts[i][0];
                string[] pys = sParts[i].Substring(colon + 1).Split(',');
                sm[ch] = ExpandSyllables(pys);
            }
            specialMap = sm;

            string[] gParts = GbkSyllableTable.Split('|');
            var codes = new List<int>(gParts.Length);
            var pysList = new List<string>(gParts.Length);
            if (gbkEnc != null)
            {
                for (int i = 0; i < gParts.Length; i++)
                {
                    int colon = gParts[i].IndexOf(':');
                    if (colon <= 0) continue;
                    string chStr = gParts[i].Substring(0, colon);
                    string py = gParts[i].Substring(colon + 1);
                    try
                    {
                        byte[] b = gbkEnc.GetBytes(chStr);
                        if (b.Length == 2)
                        {
                            codes.Add(b[0] * 256 + b[1]);
                            pysList.Add(py);
                        }
                    }
                    catch { }
                }
            }
            gbkCodes = codes.ToArray();
            gbkPinyins = pysList.ToArray();
            coreInited = true;
        }
    }

    static string LookupGbkLevel1(int code)
    {
        if (gbkCodes == null || gbkCodes.Length == 0 || code < 45217 || code > 55289) return null;
        int idx = Array.BinarySearch(gbkCodes, code);
        if (idx < 0) idx = (~idx) - 1;
        if (idx >= 0 && idx < gbkPinyins.Length) return gbkPinyins[idx];
        return null;
    }

    static void EnsureZhFallbackInit()
    {
        if (zhInited) return;
        lock (syncLock)
        {
            if (zhInited) return;
            try
            {
                zhCompare = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;
                if (gbkEnc != null && gbkPinyins != null && gbkPinyins.Length > 0)
                {
                    var minAnchor = new string[gbkPinyins.Length];
                    byte[] pair = new byte[2];
                    for (int hi = 0xB0; hi <= 0xD7; hi++)
                    {
                        int loMax = (hi == 0xD7) ? 0xF9 : 0xFE;
                        pair[0] = (byte)hi;
                        for (int lo = 0xA1; lo <= loMax; lo++)
                        {
                            pair[1] = (byte)lo;
                            string chStr = gbkEnc.GetString(pair);
                            if (string.IsNullOrEmpty(chStr)) continue;
                            char ch = chStr[0];
                            if (specialMap != null && specialMap.ContainsKey(ch)) continue;
                            int code = hi * 256 + lo;
                            int sIdx = Array.BinarySearch(gbkCodes, code);
                            if (sIdx < 0) sIdx = (~sIdx) - 1;
                            if (sIdx < 0 || sIdx >= minAnchor.Length) continue;
                            if (minAnchor[sIdx] == null || zhCompare.Compare(chStr, minAnchor[sIdx]) < 0)
                                minAnchor[sIdx] = chStr;
                        }
                    }
                    var list = new List<ZhAnchor>(minAnchor.Length);
                    for (int i = 0; i < minAnchor.Length; i++)
                    {
                        if (minAnchor[i] != null)
                            list.Add(new ZhAnchor { Anchor = minAnchor[i], Pinyin = gbkPinyins[i] });
                    }
                    list.Sort(delegate(ZhAnchor a, ZhAnchor b) { return zhCompare.Compare(a.Anchor, b.Anchor); });
                    zhAnchors = list.ToArray();
                }
            }
            catch { zhAnchors = null; }
            zhInited = true;
        }
    }

    static string[] GetCharSyllables(char c)
    {
        EnsureCoreInit();
        lock (syncLock)
        {
            string[] cached;
            if (charCache.TryGetValue(c, out cached)) return cached;
        }

        string[] result = null;
        string[] sp;
        if (specialMap != null && specialMap.TryGetValue(c, out sp))
        {
            result = sp;
        }
        else if (gbkEnc != null)
        {
            try
            {
                byte[] b = gbkEnc.GetBytes(new char[] { c });
                if (b.Length == 2)
                {
                    int code = b[0] * 256 + b[1];
                    string py = LookupGbkLevel1(code);
                    if (py != null) result = ExpandSyllables(new string[] { py });
                }
            }
            catch { }
        }

        if (result == null && c >= '\u4E00' && c <= '\u9FFF')
        {
            EnsureZhFallbackInit();
            if (zhCompare != null && zhAnchors != null && zhAnchors.Length > 0)
            {
                string key = new string(c, 1);
                int lo = 0, hi = zhAnchors.Length - 1, found = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    int cmp = zhCompare.Compare(zhAnchors[mid].Anchor, key);
                    if (cmp <= 0) { found = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
                if (found >= 0)
                    result = ExpandSyllables(new string[] { zhAnchors[found].Pinyin });
            }
        }

        lock (syncLock)
        {
            if (result != null) charCache[c] = result;
            else charCache[c] = new string[0];
        }
        return result ?? new string[0];
    }

    public static PinyinTitle BuildTitle(string text)
    {
        if (string.IsNullOrEmpty(text)) return PinyinTitle.Empty;
        var units = new List<string[]>(text.Length);
        var initials = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < 128)
            {
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    units.Add(new string[] { new string(c, 1) });
                    initials.Append(c);
                }
                continue;
            }
            string[] syls = GetCharSyllables(c);
            if (syls.Length > 0)
            {
                units.Add(syls);
                if (!string.IsNullOrEmpty(syls[0])) initials.Append(syls[0][0]);
            }
        }
        if (units.Count == 0) return PinyinTitle.Empty;
        return new PinyinTitle(units.ToArray(), initials.ToString());
    }

    public static string GetInitials(string text)
    {
        return BuildTitle(text).Initials;
    }

    static bool StartsWithAt(string target, int tPos, string prefix, int pPos, int len)
    {
        if (tPos + len > target.Length || pPos + len > prefix.Length) return false;
        for (int i = 0; i < len; i++)
            if (target[tPos + i] != prefix[pPos + i]) return false;
        return true;
    }

    static bool IsDigraph(string syl)
    {
        return syl.Length >= 2 && syl[1] == 'h' && (syl[0] == 'z' || syl[0] == 'c' || syl[0] == 's');
    }

    static bool UnitStartsWithChar(string[] unit, char ch)
    {
        for (int i = 0; i < unit.Length; i++)
            if (unit[i].Length > 0 && unit[i][0] == ch) return true;
        return false;
    }

    static void DfsMatch(string[][] units, int uIdx, string q, int qPos, bool usedFull, bool[] visited, ref bool matched, ref bool bestFull)
    {
        if (bestFull) return;
        if (uIdx >= units.Length || qPos >= q.Length) return;

        int vKey = ((uIdx * q.Length) + qPos) * 2 + (usedFull ? 1 : 0);
        if (visited[vKey]) return;
        visited[vKey] = true;

        string[] syls = units[uIdx];
        int rem = q.Length - qPos;

        for (int s = 0; s < syls.Length; s++)
        {
            string syl = syls[s];
            if (syl.Length == 0 || syl[0] != q[qPos]) continue;

            // 1. 末尾音节/前缀直中
            if (rem <= syl.Length && StartsWithAt(syl, 0, q, qPos, rem))
            {
                matched = true;
                if (usedFull || rem > 1) { bestFull = true; return; }
            }

            // 2. 完整音节续接下一字
            if (qPos + syl.Length < q.Length && StartsWithAt(q, qPos, syl, 0, syl.Length))
            {
                DfsMatch(units, uIdx + 1, q, qPos + syl.Length, usedFull || syl.Length > 1, visited, ref matched, ref bestFull);
                if (bestFull) return;
            }

            // 3. zh / ch / sh 双字母声母续接
            if (syl.Length > 2 && IsDigraph(syl) && qPos + 2 < q.Length && q[qPos + 1] == syl[1])
            {
                DfsMatch(units, uIdx + 1, q, qPos + 2, true, visited, ref matched, ref bestFull);
                if (bestFull) return;
            }

            // 4. 单字母首字母续接
            if (syl.Length > 1 && qPos + 1 < q.Length)
            {
                DfsMatch(units, uIdx + 1, q, qPos + 1, usedFull, visited, ref matched, ref bestFull);
                if (bestFull) return;
            }
        }
    }

    public static int MatchScore(PinyinTitle pt, string query)
    {
        if (pt == null || pt.Units.Length == 0 || string.IsNullOrEmpty(query) || query.Length > 32)
            return 0;

        char[] qa = new char[query.Length];
        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];
            if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))) return 0;
            qa[i] = c;
        }
        string q = new string(qa);

        string[][] units = pt.Units;
        int bestScore = 0;
        bool[] visited = null;

        for (int start = 0; start < units.Length; start++)
        {
            if (!UnitStartsWithChar(units[start], q[0])) continue;
            if (visited == null) visited = new bool[units.Length * q.Length * 2];
            else Array.Clear(visited, 0, visited.Length);

            bool matched = false;
            bool bestFull = false;
            DfsMatch(units, start, q, 0, false, visited, ref matched, ref bestFull);
            if (matched)
            {
                int sc = (start == 0) ? (bestFull ? 86 : 80) : (bestFull ? 72 : 65);
                if (sc > bestScore) bestScore = sc;
                if (bestScore >= 86) break;
            }
        }
        return bestScore;
    }

    public static int MatchScore(string text, string query)
    {
        return MatchScore(BuildTitle(text), query);
    }

    public static bool Matches(PinyinTitle pt, string query)
    {
        return MatchScore(pt, query) > 0;
    }
}

public class PaletteEntry
{
    public string PinKey;       // 唯一键：SourceTab + "\u0001" + Title
    public string SourceTab;
    public string Title;
    public string Body;
    public string Alt;
    public string Ulx;
    public string Note;
    public string TitlePy;
    public PinyinTitle TitlePinyin;
    public bool IsCustom;
    public bool IsPinned;
    public int RecentOrder;     // 0=最近第1条, -1=非最近
}

/// <summary>
/// 全局命令面板（Alt+K / 全局热键唤起）：支持居中/左侧/右侧三档覆盖层与跨全库智能拼音瞬搜。
/// </summary>
public class CommandPaletteForm : Form
{
    public const int DefaultOpacityPct = 86;
    public const int MinOpacityPct = 55;
    public const int MaxOpacityPct = 100;

    public const int DockCenter = 0;
    public const int DockLeft = 1;
    public const int DockRight = 2;

    static readonly int[] OpacityPresets = { 86, 74, 62, 94, 100 };

    public static int ClampOpacityPct(int pct)
    {
        return Math.Max(MinOpacityPct, Math.Min(MaxOpacityPct, pct));
    }

    public static int NextOpacityPreset(int currentPct)
    {
        for (int i = 0; i < OpacityPresets.Length; i++)
        {
            if (OpacityPresets[i] == currentPct)
                return OpacityPresets[(i + 1) % OpacityPresets.Length];
        }
        return DefaultOpacityPct;
    }

    public static int NormalizeDockMode(int mode)
    {
        return (mode == DockLeft || mode == DockRight) ? mode : DockCenter;
    }

    public static int NextDockMode(int mode)
    {
        return (NormalizeDockMode(mode) + 1) % 3;
    }

    public static string DockModeLabel(int mode)
    {
        int m = NormalizeDockMode(mode);
        if (m == DockLeft) return "左侧";
        if (m == DockRight) return "右侧";
        return "居中";
    }

    public static Rectangle ComputeDockBounds(Rectangle scr, int mode)
    {
        int m = NormalizeDockMode(mode);
        if (m == DockCenter)
        {
            int w = 700, h = 470;
            int x = scr.X + (scr.Width - w) / 2;
            int y = scr.Y + Math.Max(48, (scr.Height - h) / 3);
            return new Rectangle(x, y, w, h);
        }

        const int margin = 12;
        int sw = Math.Min(456, Math.Max(380, scr.Width - margin * 2));
        int sh = Math.Min(scr.Height - margin * 2, Math.Max(520, Math.Min(920, (int)Math.Round(scr.Height * 0.84))));
        int sy = scr.Y + (scr.Height - sh) / 2;
        int sx = (m == DockLeft) ? (scr.X + margin) : (scr.Right - sw - margin);
        return new Rectangle(sx, sy, sw, sh);
    }

    readonly List<PaletteEntry> allEntries;
    readonly Func<PaletteEntry, bool, string> resolveCmd;
    readonly Action<PaletteEntry, bool> onExecute;
    readonly Func<string, bool> onTogglePin;
    readonly Action<int> onOpacityChanged;
    readonly Action<int> onDockChanged;
    readonly bool openedFromGlobal;
    readonly string currentTarget;

    TextBox txtQuery;
    RxSegmentedControl segMode;
    Panel listPanel;
    Label lblCount;

    List<PaletteEntry> matches = new List<PaletteEntry>();
    string[] curTokens = new string[0];
    int selIdx = 0;
    int hoverIdx = -1;
    int scrollIdx = 0;
    bool ulxMode = false;
    int opacityPct = DefaultOpacityPct;
    int dockMode = DockCenter;
    bool useDwmRound = false;
    bool opacityPillHover = false;
    bool dockPillHover = false;

    int shownTick = 0;
    bool bounceRecovered = false;

    System.Windows.Forms.Timer slideTimer;
    int slideStartTick;
    int slideStartX;
    int slideTargetX;
    int slideTargetY;
    double slideTargetOpacity;

    Font fQuery, fTitle, fCode, fBadge, fFooter;

    bool IsSideMode { get { return dockMode != DockCenter; } }
    int CurrentRowH { get { return IsSideMode ? 60 : 44; } }

    Rectangle DockPillRect
    {
        get
        {
            return IsSideMode
                ? new Rectangle(Width - 166, Height - 28, 72, 20)
                : new Rectangle(Width - 328, Height - 26, 72, 20);
        }
    }

    Rectangle OpacityPillRect
    {
        get
        {
            return IsSideMode
                ? new Rectangle(Width - 90, Height - 28, 78, 20)
                : new Rectangle(Width - 252, Height - 26, 78, 20);
        }
    }

    public CommandPaletteForm(
        List<PaletteEntry> entries,
        string targetName,
        Func<PaletteEntry, bool, string> resolver,
        Action<PaletteEntry, bool> executeCb,
        Func<string, bool> togglePinCb)
        : this(entries, targetName, resolver, executeCb, togglePinCb, DefaultOpacityPct, null, DockCenter, null, false)
    {
    }

    public CommandPaletteForm(
        List<PaletteEntry> entries,
        string targetName,
        Func<PaletteEntry, bool, string> resolver,
        Action<PaletteEntry, bool> executeCb,
        Func<string, bool> togglePinCb,
        int initialOpacityPct,
        Action<int> opacityChangedCb)
        : this(entries, targetName, resolver, executeCb, togglePinCb, initialOpacityPct, opacityChangedCb, DockCenter, null, false)
    {
    }

    public CommandPaletteForm(
        List<PaletteEntry> entries,
        string targetName,
        Func<PaletteEntry, bool, string> resolver,
        Action<PaletteEntry, bool> executeCb,
        Func<string, bool> togglePinCb,
        int initialOpacityPct,
        Action<int> opacityChangedCb,
        int initialDockMode,
        Action<int> dockChangedCb,
        bool fromGlobalHotkey)
    {
        allEntries = entries ?? new List<PaletteEntry>();
        currentTarget = targetName ?? "";
        resolveCmd = resolver;
        onExecute = executeCb;
        onTogglePin = togglePinCb;
        onOpacityChanged = opacityChangedCb;
        onDockChanged = dockChangedCb;
        openedFromGlobal = fromGlobalHotkey;
        opacityPct = ClampOpacityPct(initialOpacityPct);
        dockMode = NormalizeDockMode(initialDockMode);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = ThemePalette.Elevated;
        Opacity = opacityPct / 100.0;

        fQuery = new Font("Microsoft YaHei UI", 12f, FontStyle.Regular);
        fTitle = new Font("Microsoft YaHei UI", 9.6f, FontStyle.Bold);
        fCode = new Font("Consolas", 8.6f, FontStyle.Regular);
        fBadge = new Font("Microsoft YaHei UI", 7.8f, FontStyle.Bold);
        fFooter = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Regular);

        // 顶部搜索输入
        txtQuery = new TextBox();
        txtQuery.BorderStyle = BorderStyle.None;
        txtQuery.Font = fQuery;
        txtQuery.BackColor = ThemePalette.Elevated;
        txtQuery.ForeColor = ThemePalette.TextPrimary;
        txtQuery.HandleCreated += delegate { UpdateCueBanner(); };
        txtQuery.TextChanged += delegate { RunSearch(txtQuery.Text); };
        txtQuery.MouseWheel += delegate(object snd, MouseEventArgs me)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
                SetOpacityPct(opacityPct + (me.Delta > 0 ? 5 : -5));
        };
        Controls.Add(txtQuery);

        segMode = new RxSegmentedControl();
        segMode.Items = new string[] { "主指令", "ulx" };
        segMode.SelectedIndexChanged += delegate
        {
            ulxMode = (segMode.SelectedIndex == 1);
            if (listPanel != null) listPanel.Invalidate();
            txtQuery.Focus();
        };
        Controls.Add(segMode);

        lblCount = new Label();
        lblCount.Font = fFooter;
        lblCount.ForeColor = ThemePalette.TextMuted;
        lblCount.BackColor = Color.Transparent;
        lblCount.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(lblCount);

        listPanel = new DoubleBufferedPanel();
        listPanel.BackColor = ThemePalette.Surface;
        listPanel.Paint += OnListPaint;
        listPanel.MouseMove += OnListMouseMove;
        listPanel.MouseLeave += delegate { if (hoverIdx != -1) { hoverIdx = -1; listPanel.Invalidate(); } };
        listPanel.MouseDown += OnListMouseDown;
        listPanel.MouseWheel += OnListWheel;
        Controls.Add(listPanel);

        ApplyDockLayout(false);
        RunSearch("");
    }

    void UpdateCueBanner()
    {
        if (txtQuery == null || !txtQuery.IsHandleCreated) return;
        string cue = IsSideMode
            ? "搜索指令/物品/角色（xrq / jiqiren / jiqr）…"
            : "搜索全库指令与自定义（拼音如 xrq / jiqiren / jiqr）…";
        try { Native.SetCueBanner(txtQuery.Handle, cue); } catch { }
    }

    void StopSlideTimer()
    {
        if (slideTimer != null)
        {
            try { slideTimer.Stop(); } catch { }
            try { slideTimer.Dispose(); } catch { }
            slideTimer = null;
        }
    }

    void ApplyDockLayout(bool animateSlide)
    {
        StopSlideTimer();

        Rectangle scr = Native.GetTargetScreenBounds();
        Rectangle target = ComputeDockBounds(scr, dockMode);
        Size = target.Size;

        if (!useDwmRound && IsHandleCreated)
            Native.ApplyRoundRegion(this, 12);

        if (IsSideMode)
        {
            txtQuery.SetBounds(40, 14, Width - 54, 24);
            segMode.SetBounds(12, 48, 116, 26);
            lblCount.SetBounds(Width - 62, 50, 50, 22);
            listPanel.SetBounds(6, 86, Width - 12, Height - 126);
        }
        else
        {
            txtQuery.SetBounds(42, 16, Width - 232, 26);
            segMode.SetBounds(Width - 180, 13, 120, 28);
            lblCount.SetBounds(Width - 58, 16, 48, 22);
            listPanel.SetBounds(6, 56, Width - 12, Height - 92);
        }
        UpdateCueBanner();

        if (animateSlide && IsSideMode && IsHandleCreated)
        {
            slideTargetX = target.X;
            slideTargetY = target.Y;
            slideStartX = (dockMode == DockLeft) ? (target.X - 150) : (target.X + 150);
            slideTargetOpacity = opacityPct / 100.0;
            double startOp = Math.Max(0.25, slideTargetOpacity * 0.45);
            Location = new Point(slideStartX, slideTargetY);
            Opacity = startOp;
            slideStartTick = Environment.TickCount;

            slideTimer = new System.Windows.Forms.Timer();
            slideTimer.Interval = 15;
            slideTimer.Tick += delegate
            {
                if (IsDisposed || !IsHandleCreated) { StopSlideTimer(); return; }
                int el = unchecked(Environment.TickCount - slideStartTick);
                double t = Math.Max(0.0, Math.Min(1.0, el / 120.0));
                double ease = 1.0 - Math.Pow(1.0 - t, 3.0);
                int curX = slideStartX + (int)Math.Round((slideTargetX - slideStartX) * ease);
                Location = new Point(curX, slideTargetY);
                Opacity = startOp + (slideTargetOpacity - startOp) * ease;
                if (t >= 1.0)
                {
                    Location = new Point(slideTargetX, slideTargetY);
                    Opacity = opacityPct / 100.0;
                    StopSlideTimer();
                }
            };
            slideTimer.Start();
        }
        else
        {
            Location = target.Location;
            Opacity = opacityPct / 100.0;
        }

        EnsureSelVisible();
        Invalidate();
        listPanel.Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        useDwmRound = Native.TrySetDwmRoundedCorners(Handle, ThemePalette.Accent);
        if (!useDwmRound) Native.ApplyRoundRegion(this, 12);
    }

    public void SetOpacityPct(int pct)
    {
        int clamped = ClampOpacityPct(pct);
        if (clamped == opacityPct) return;
        opacityPct = clamped;
        slideTargetOpacity = opacityPct / 100.0;
        if (slideTimer == null) Opacity = slideTargetOpacity;
        Invalidate();
        if (onOpacityChanged != null) onOpacityChanged(opacityPct);
    }

    class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        shownTick = Environment.TickCount;
        if (IsSideMode) ApplyDockLayout(true);
        if (openedFromGlobal) Native.ForceForegroundWindow(Handle);
        else { Native.MakeTopmost(Handle); Activate(); }
        txtQuery.Focus();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (openedFromGlobal && !bounceRecovered && unchecked(Environment.TickCount - shownTick) < 160)
        {
            bounceRecovered = true;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && Visible)
                    {
                        Native.ForceForegroundWindow(Handle);
                        txtQuery.Focus();
                    }
                });
            }
            catch { }
            return;
        }
        try { Close(); } catch { }
    }

    public static List<PaletteEntry> ScoreAndFilter(List<PaletteEntry> entries, string query, int maxCount)
    {
        string q = (query ?? "").Trim();
        string[] tokens = q.Length == 0
            ? new string[0]
            : q.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var scored = new List<KeyValuePair<int, PaletteEntry>>();

        if (tokens.Length == 0)
        {
            // 空查询：星标收藏（最近用过的更靠前）> 最近使用 > 其余条目按原序补齐至上限
            int normalAdded = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                PaletteEntry e = entries[i];
                if (e.IsPinned)
                {
                    int sc = (e.RecentOrder >= 0) ? (300000 - e.RecentOrder) : (250000 - i);
                    scored.Add(new KeyValuePair<int, PaletteEntry>(sc, e));
                }
                else if (e.RecentOrder >= 0)
                {
                    scored.Add(new KeyValuePair<int, PaletteEntry>(200000 - e.RecentOrder, e));
                }
                else if (normalAdded < maxCount)
                {
                    scored.Add(new KeyValuePair<int, PaletteEntry>(100000 - i, e));
                    normalAdded++;
                }
            }
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                PaletteEntry e = entries[i];
                int score = 0;
                bool allHit = true;
                for (int t = 0; t < tokens.Length; t++)
                {
                    string kw = tokens[t];
                    int best = 0;
                    if (e.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) == 0) best = 120;
                    else if (e.Title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 90;
                    else
                    {
                        PinyinTitle pt = e.TitlePinyin ?? (e.TitlePinyin = PinyinHelper.BuildTitle(e.Title));
                        int pyScore = PinyinHelper.MatchScore(pt, kw);
                        if (pyScore > 0) best = pyScore;
                        else if (e.Body.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 55;
                        else if (e.Alt.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 45;
                        else if (e.Ulx.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 45;
                        else if (e.Note.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 35;
                        else if (e.SourceTab.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) best = 20;
                    }

                    if (best == 0) { allHit = false; break; }
                    score += best;
                }
                if (!allHit) continue;
                if (e.IsPinned) score += 28;
                if (e.RecentOrder >= 0) score += Math.Max(4, 20 - e.RecentOrder * 2);
                scored.Add(new KeyValuePair<int, PaletteEntry>(score, e));
            }
        }

        scored.Sort(delegate(KeyValuePair<int, PaletteEntry> a, KeyValuePair<int, PaletteEntry> b)
        {
            return b.Key.CompareTo(a.Key);
        });

        var result = new List<PaletteEntry>(Math.Min(maxCount, scored.Count));
        for (int i = 0; i < scored.Count && i < maxCount; i++)
            result.Add(scored[i].Value);
        return result;
    }

    void RunSearch(string query)
    {
        string q = (query ?? "").Trim();
        curTokens = q.Length == 0 ? new string[0] : q.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        matches = ScoreAndFilter(allEntries, q, 120);
        selIdx = matches.Count > 0 ? 0 : -1;
        scrollIdx = 0;
        lblCount.Text = matches.Count + " 项";
        listPanel.Invalidate();
    }

    int VisibleRows { get { return Math.Max(1, listPanel.ClientSize.Height / CurrentRowH); } }

    void EnsureSelVisible()
    {
        if (selIdx < 0) return;
        if (selIdx < scrollIdx) scrollIdx = selIdx;
        else if (selIdx >= scrollIdx + VisibleRows) scrollIdx = selIdx - VisibleRows + 1;
        int maxSc = Math.Max(0, matches.Count - VisibleRows);
        if (scrollIdx > maxSc) scrollIdx = maxSc;
        if (scrollIdx < 0) scrollIdx = 0;
    }

    void ExecuteSelected(bool forceUlx)
    {
        if (selIdx < 0 || selIdx >= matches.Count) return;
        PaletteEntry item = matches[selIdx];
        bool useU = forceUlx || ulxMode;
        Close();
        if (onExecute != null) onExecute(item, useU);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool shift = (keyData & Keys.Shift) != 0;
        bool ctrl = (keyData & Keys.Control) != 0;

        if (key == Keys.Escape)
        {
            Close();
            return true;
        }
        if (key == Keys.Down)
        {
            if (matches.Count > 0)
            {
                selIdx = (selIdx + 1) % matches.Count;
                EnsureSelVisible();
                listPanel.Invalidate();
            }
            return true;
        }
        if (key == Keys.Up)
        {
            if (matches.Count > 0)
            {
                selIdx = (selIdx <= 0) ? (matches.Count - 1) : (selIdx - 1);
                EnsureSelVisible();
                listPanel.Invalidate();
            }
            return true;
        }
        if (key == Keys.Enter)
        {
            ExecuteSelected(shift);
            return true;
        }
        if (key == Keys.Tab)
        {
            ulxMode = !ulxMode;
            segMode.SetSelectedSilent(ulxMode ? 1 : 0);
            listPanel.Invalidate();
            return true;
        }
        if (ctrl && key == Keys.P)
        {
            if (selIdx >= 0 && selIdx < matches.Count && onTogglePin != null)
            {
                matches[selIdx].IsPinned = onTogglePin(matches[selIdx].PinKey);
                listPanel.Invalidate();
            }
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    static void DrawFooterPill(Graphics g, Font font, Rectangle rc, string text, bool hovered)
    {
        using (var bg = new SolidBrush(hovered ? ThemePalette.Hover : ThemePalette.CodeChipBg))
            Native.FillRoundedRect(g, bg, rc, 5);
        using (var edge = new Pen(hovered ? ThemePalette.Accent : ThemePalette.CodeChipEdge, 1f))
            Native.DrawRoundedRect(g, edge, rc, 5);
        TextRenderer.DrawText(g, text, font, rc,
            hovered ? ThemePalette.AccentSoftFg : ThemePalette.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ThemePalette.Elevated);

        bool side = IsSideMode;
        string tgt = string.IsNullOrEmpty(currentTarget) ? "目标：^ (自己)" : ("目标：" + currentTarget);

        // 搜索放大镜图标
        Rectangle searchRc = side ? new Rectangle(14, 15, 18, 18) : new Rectangle(15, 17, 18, 18);
        UiIcons.Draw(g, NavIconKind.Search, searchRc, ThemePalette.Accent, false);

        // 顶部与底部分割线
        int topSepY = side ? 82 : 52;
        int botSepY = side ? (Height - 36) : (Height - 32);
        using (var sep = new Pen(ThemePalette.BorderSubtle, 1f))
        {
            g.DrawLine(sep, 0, topSepY, Width, topSepY);
            g.DrawLine(sep, 0, botSepY, Width, botSepY);
        }

        if (side)
        {
            TextRenderer.DrawText(g, tgt, fFooter, new Rectangle(134, 48, Math.Max(40, Width - 200), 26),
                string.IsNullOrEmpty(currentTarget) ? ThemePalette.TextMuted : ThemePalette.AccentSoftFg,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "↑↓ · Enter 复制 · Shift+Enter ulx · Tab",
                fFooter, new Rectangle(12, Height - 34, Math.Max(60, Width - 182), 30), ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        else
        {
            TextRenderer.DrawText(g, "↑↓ 选 · Enter 复制 · Shift+Enter ulx · Ctrl+P 收藏 · Tab 写法",
                fFooter, new Rectangle(12, Height - 30, Math.Max(80, Width - 344), 26), ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, tgt, fFooter, new Rectangle(Width - 170, Height - 30, 158, 26),
                string.IsNullOrEmpty(currentTarget) ? ThemePalette.TextMuted : ThemePalette.AccentSoftFg,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        DrawFooterPill(g, fFooter, DockPillRect, "位置: " + DockModeLabel(dockMode), dockPillHover);
        string opTxt = (opacityPct >= 100) ? "不透明 100%" : ("半透明 " + opacityPct + "%");
        DrawFooterPill(g, fFooter, OpacityPillRect, opTxt, opacityPillHover);

        // 外轮廓圆角描边
        using (var pen = new Pen(ThemePalette.Accent, 1.4f))
            Native.DrawRoundedRect(g, pen, new Rectangle(0, 0, Width - 1, Height - 1), useDwmRound ? 8 : 12);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hitDock = DockPillRect.Contains(e.Location);
        bool hitOp = OpacityPillRect.Contains(e.Location);
        if (hitDock != dockPillHover)
        {
            dockPillHover = hitDock;
            Invalidate(DockPillRect);
        }
        if (hitOp != opacityPillHover)
        {
            opacityPillHover = hitOp;
            Invalidate(OpacityPillRect);
        }
        Cursor = (hitDock || hitOp) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (dockPillHover)
        {
            dockPillHover = false;
            Invalidate(DockPillRect);
        }
        if (opacityPillHover)
        {
            opacityPillHover = false;
            Invalidate(OpacityPillRect);
        }
        Cursor = Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (DockPillRect.Contains(e.Location))
        {
            dockMode = NextDockMode(dockMode);
            dockPillHover = false;
            opacityPillHover = false;
            Cursor = Cursors.Default;
            ApplyDockLayout(true);
            if (onDockChanged != null) onDockChanged(dockMode);
            txtQuery.Focus();
            return;
        }
        if (OpacityPillRect.Contains(e.Location))
        {
            SetOpacityPct(NextOpacityPreset(opacityPct));
            txtQuery.Focus();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if ((Control.ModifierKeys & Keys.Control) != 0)
        {
            SetOpacityPct(opacityPct + (e.Delta > 0 ? 5 : -5));
            return;
        }
        base.OnMouseWheel(e);
    }

    void OnListPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ThemePalette.Surface);
        int w = listPanel.ClientSize.Width;
        int h = listPanel.ClientSize.Height;

        if (matches.Count == 0)
        {
            TextRenderer.DrawText(g, "没有找到匹配指令（支持全拼/简拼、代码、备注跨表搜索）", fTitle,
                new Rectangle(20, 40, w - 40, 32), ThemePalette.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }

        bool side = IsSideMode;
        int rh = CurrentRowH;
        int vis = VisibleRows + 1;
        for (int vi = 0; vi < vis; vi++)
        {
            int idx = scrollIdx + vi;
            if (idx >= matches.Count) break;
            int y = vi * rh;
            if (y >= h) break;

            PaletteEntry item = matches[idx];
            bool sel = (idx == selIdx);
            bool hov = (!sel && idx == hoverIdx);
            Rectangle rowRc = side
                ? new Rectangle(6, y + 3, w - 12, rh - 6)
                : new Rectangle(4, y + 2, w - 8, rh - 4);
            int rowRad = side ? 8 : 7;

            if (sel)
            {
                using (var sb = new SolidBrush(ThemePalette.Selected))
                    Native.FillRoundedRect(g, sb, rowRc, rowRad);
                using (var ab = new SolidBrush(ThemePalette.Accent))
                    Native.FillRoundedRect(g, ab, new Rectangle(rowRc.X + 2, rowRc.Y + 8, 3, rowRc.Height - 16), 2);
            }
            else if (hov)
            {
                using (var hb = new SolidBrush(ThemePalette.Hover))
                    Native.FillRoundedRect(g, hb, rowRc, rowRad);
            }

            Color bbg, bfg;
            ThemePalette.GetSourceBadgeColors(item.SourceTab, out bbg, out bfg);
            string tagName = item.SourceTab.Replace("表", "").Replace("角色", "角色");
            if (tagName.Length > 4) tagName = tagName.Substring(0, 4);

            string cmd = (resolveCmd != null) ? resolveCmd(item, ulxMode) : item.Body;
            int nl = cmd.IndexOfAny(new char[] { '\r', '\n' });
            if (nl >= 0) cmd = cmd.Substring(0, nl) + " …";
            int maxCmdLen = side ? 64 : 46;
            if (cmd.Length > maxCmdLen) cmd = cmd.Substring(0, maxCmdLen) + "…";
            Size csz = TextRenderer.MeasureText(g, cmd, fCode);

            if (side)
            {
                // 侧边双行精致卡片：上行星标+徽标+标题，下行完整命令胶囊+备注
                Rectangle starRc = new Rectangle(rowRc.X + 10, rowRc.Y + 9, 14, 14);
                if (item.IsPinned || sel || hov)
                    UiIcons.Draw(g, NavIconKind.Star, starRc, item.IsPinned ? ThemePalette.StarActive : ThemePalette.TextMuted, item.IsPinned);

                Rectangle badgeRc = new Rectangle(rowRc.X + 29, rowRc.Y + 6, 44, 19);
                using (var bb = new SolidBrush(bbg))
                    Native.FillRoundedRect(g, bb, badgeRc, 5);
                TextRenderer.DrawText(g, tagName, fBadge, badgeRc, bfg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                Rectangle titleRc = new Rectangle(badgeRc.Right + 8, rowRc.Y + 5, Math.Max(40, rowRc.Right - badgeRc.Right - 18), 21);
                TextRenderer.DrawText(g, item.Title, fTitle, titleRc,
                    sel ? ThemePalette.AccentSoftFg : ThemePalette.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                int maxChipW = Math.Max(80, rowRc.Width - 39);
                int chipW = Math.Min(maxChipW, Math.Max(68, csz.Width + 14));
                Rectangle chipRc = new Rectangle(rowRc.X + 29, rowRc.Y + 29, chipW, 21);
                using (var cb = new SolidBrush(ThemePalette.CodeChipBg))
                    Native.FillRoundedRect(g, cb, chipRc, 5);
                using (var ce = new Pen(sel ? ThemePalette.Accent : ThemePalette.CodeChipEdge, 1f))
                    Native.DrawRoundedRect(g, ce, chipRc, 5);
                TextRenderer.DrawText(g, cmd, fCode, new Rectangle(chipRc.X + 6, chipRc.Y, chipRc.Width - 12, chipRc.Height),
                    ThemePalette.CodeChipFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                if (!string.IsNullOrEmpty(item.Note))
                {
                    int noteX = chipRc.Right + 8;
                    int noteW = rowRc.Right - noteX - 10;
                    if (noteW >= 56)
                    {
                        TextRenderer.DrawText(g, "· " + item.Note, fFooter, new Rectangle(noteX, chipRc.Y, noteW, chipRc.Height),
                            ThemePalette.TextMuted,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    }
                }
            }
            else
            {
                // 居中单行高密度横排
                Rectangle starRc = new Rectangle(rowRc.X + 10, rowRc.Y + (rowRc.Height - 14) / 2, 14, 14);
                if (item.IsPinned || sel || hov)
                    UiIcons.Draw(g, NavIconKind.Star, starRc, item.IsPinned ? ThemePalette.StarActive : ThemePalette.TextMuted, item.IsPinned);

                Rectangle badgeRc = new Rectangle(rowRc.X + 30, rowRc.Y + (rowRc.Height - 20) / 2, 48, 20);
                using (var bb = new SolidBrush(bbg))
                    Native.FillRoundedRect(g, bb, badgeRc, 5);
                TextRenderer.DrawText(g, tagName, fBadge, badgeRc, bfg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                int chipW = Math.Min(280, Math.Max(60, csz.Width + 14));
                Rectangle chipRc = new Rectangle(rowRc.Right - chipW - 8, rowRc.Y + (rowRc.Height - 24) / 2, chipW, 24);
                using (var cb = new SolidBrush(ThemePalette.CodeChipBg))
                    Native.FillRoundedRect(g, cb, chipRc, 5);
                using (var ce = new Pen(sel ? ThemePalette.Accent : ThemePalette.CodeChipEdge, 1f))
                    Native.DrawRoundedRect(g, ce, chipRc, 5);
                TextRenderer.DrawText(g, cmd, fCode, new Rectangle(chipRc.X + 6, chipRc.Y, chipRc.Width - 12, chipRc.Height),
                    ThemePalette.CodeChipFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                int tx = badgeRc.Right + 10;
                int tw = Math.Max(60, chipRc.Left - tx - 10);
                string titleLine = item.Title;
                if (!string.IsNullOrEmpty(item.Note)) titleLine += "  ·  " + item.Note;
                TextRenderer.DrawText(g, titleLine, fTitle, new Rectangle(tx, rowRc.Y, tw, rowRc.Height),
                    sel ? ThemePalette.AccentSoftFg : ThemePalette.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }

    void OnListMouseMove(object sender, MouseEventArgs e)
    {
        int idx = scrollIdx + (e.Y / CurrentRowH);
        if (idx < 0 || idx >= matches.Count) idx = -1;
        if (idx != hoverIdx)
        {
            hoverIdx = idx;
            listPanel.Cursor = (idx >= 0) ? Cursors.Hand : Cursors.Default;
            listPanel.Invalidate();
        }
    }

    void OnListMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        int rh = CurrentRowH;
        int idx = scrollIdx + (e.Y / rh);
        if (idx < 0 || idx >= matches.Count) return;
        selIdx = idx;
        bool hitStar = IsSideMode ? (e.X <= 34 && (e.Y % rh) <= 30) : (e.X <= 30);
        if (hitStar && onTogglePin != null)
        {
            matches[idx].IsPinned = onTogglePin(matches[idx].PinKey);
            listPanel.Invalidate();
            return;
        }
        ExecuteSelected((Control.ModifierKeys & Keys.Shift) != 0);
    }

    void OnListWheel(object sender, MouseEventArgs e)
    {
        if ((Control.ModifierKeys & Keys.Control) != 0)
        {
            SetOpacityPct(opacityPct + (e.Delta > 0 ? 5 : -5));
            return;
        }
        int delta = (e.Delta > 0) ? -2 : 2;
        int maxSc = Math.Max(0, matches.Count - VisibleRows);
        scrollIdx = Math.Max(0, Math.Min(maxSc, scrollIdx + delta));
        listPanel.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopSlideTimer();
            if (fQuery != null) { fQuery.Dispose(); fQuery = null; }
            if (fTitle != null) { fTitle.Dispose(); fTitle = null; }
            if (fCode != null) { fCode.Dispose(); fCode = null; }
            if (fBadge != null) { fBadge.Dispose(); fBadge = null; }
            if (fFooter != null) { fFooter.Dispose(); fFooter = null; }
        }
        base.Dispose(disposing);
    }
}
#endif
