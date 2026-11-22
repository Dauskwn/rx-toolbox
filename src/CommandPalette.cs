#if ADMIN_EDITION
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

public class PalettePlayerContext
{
    public List<OnlineServerEntry> Servers;
    public string InitialServerAddr = "";
    public string MySteamName = "";
    public bool HideBots = true;
    public int SortMode = 0;
    public Dictionary<string, OnlinePlayerCache> Cache;
    public Action<string> OnTargetChanged;
    public Action<string, bool> OnCopyPlayerName;
    public Action<string> OnServerChanged;
}

internal sealed class PalettePlayerRow
{
    public bool IsResetRow;
    public A2SPlayerInfo Player;
}

internal sealed class PaletteMenuColorTable : ProfessionalColorTable
{
    public override Color MenuBorder { get { return ThemePalette.BorderSubtle; } }
    public override Color MenuItemBorder { get { return ThemePalette.Accent; } }
    public override Color MenuItemSelected { get { return ThemePalette.Hover; } }
    public override Color MenuStripGradientBegin { get { return ThemePalette.SurfaceAlt; } }
    public override Color MenuStripGradientEnd { get { return ThemePalette.SurfaceAlt; } }
    public override Color ToolStripDropDownBackground { get { return ThemePalette.SurfaceAlt; } }
    public override Color ImageMarginGradientBegin { get { return ThemePalette.SurfaceAlt; } }
    public override Color ImageMarginGradientMiddle { get { return ThemePalette.SurfaceAlt; } }
    public override Color ImageMarginGradientEnd { get { return ThemePalette.SurfaceAlt; } }
    public override Color SeparatorDark { get { return ThemePalette.BorderSubtle; } }
    public override Color SeparatorLight { get { return ThemePalette.BorderSubtle; } }
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

    public static bool TryParseInlinePlayerQuery(string text, out string playerQuery)
    {
        playerQuery = "";
        if (string.IsNullOrEmpty(text)) return false;
        if (text[0] != '@') return false;
        playerQuery = text.Substring(1).TrimStart();
        return true;
    }

    public static string ShortServerLabel(string label)
    {
        string s = (label ?? "").Trim();
        if (s.Length == 0) return "选择服务器";
        int lp = s.LastIndexOf(" (", StringComparison.Ordinal);
        if (lp > 0 && s[s.Length - 1] == ')')
            s = s.Substring(0, lp).TrimEnd();
        if (s.Length > 20) s = s.Substring(0, 19) + "…";
        return s;
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
    readonly PalettePlayerContext playerCtx;
    string currentTarget;

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
    bool targetPillHover = false;
    bool serverPillHover = false;
    bool refreshPillHover = false;

    bool playerMode = false;
    bool playerModeByAt = false;
    bool suppressQueryEvent = false;
    bool suppressDeactivateClose = false;
    bool menuTriggeredPaletteClose = false;
    string savedCmdQuery = "";
    string curPlayerQuery = "";
    string currentServerAddr = "";
    int playerQuerySeq = 0;
    bool playerLoading = false;
    string playerStatusNote = "";
    List<A2SPlayerInfo> currentRawPlayers = new List<A2SPlayerInfo>();
    List<PalettePlayerRow> playerRows = new List<PalettePlayerRow>();
    ContextMenuStrip activePopupMenu;

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
    bool CanPickPlayer { get { return playerCtx != null && playerCtx.Servers != null && playerCtx.Servers.Count > 0; } }
    int CurrentRowH { get { return playerMode ? 38 : (IsSideMode ? 60 : 44); } }
    int CurrentItemCount { get { return playerMode ? playerRows.Count : matches.Count; } }

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

    Rectangle TargetPillRect
    {
        get
        {
            if (!IsSideMode)
                return new Rectangle(Width - 170, Height - 26, 158, 20);
            if (playerMode)
                return new Rectangle(196, 50, Math.Max(70, Width - 264), 22);
            return new Rectangle(136, 50, Math.Max(80, Width - 204), 22);
        }
    }

    Rectangle ServerPillRect
    {
        get
        {
            if (!playerMode) return Rectangle.Empty;
            if (!IsSideMode) return new Rectangle(Width - 230, 15, 142, 24);
            return new Rectangle(12, 50, 150, 22);
        }
    }

    Rectangle RefreshPillRect
    {
        get
        {
            if (!playerMode) return Rectangle.Empty;
            if (!IsSideMode) return new Rectangle(Width - 84, 15, 24, 24);
            return new Rectangle(166, 50, 24, 22);
        }
    }

    public CommandPaletteForm(
        List<PaletteEntry> entries,
        string targetName,
        Func<PaletteEntry, bool, string> resolver,
        Action<PaletteEntry, bool> executeCb,
        Func<string, bool> togglePinCb)
        : this(entries, targetName, resolver, executeCb, togglePinCb, DefaultOpacityPct, null, DockCenter, null, false, null)
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
        : this(entries, targetName, resolver, executeCb, togglePinCb, initialOpacityPct, opacityChangedCb, DockCenter, null, false, null)
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
        : this(entries, targetName, resolver, executeCb, togglePinCb, initialOpacityPct, opacityChangedCb, initialDockMode, dockChangedCb, fromGlobalHotkey, null)
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
        bool fromGlobalHotkey,
        PalettePlayerContext playerContext)
    {
        allEntries = entries ?? new List<PaletteEntry>();
        currentTarget = targetName ?? "";
        resolveCmd = resolver;
        onExecute = executeCb;
        onTogglePin = togglePinCb;
        onOpacityChanged = opacityChangedCb;
        onDockChanged = dockChangedCb;
        openedFromGlobal = fromGlobalHotkey;
        playerCtx = playerContext;
        if (playerCtx != null)
        {
            if (playerCtx.Cache == null)
                playerCtx.Cache = new Dictionary<string, OnlinePlayerCache>(StringComparer.OrdinalIgnoreCase);
            currentServerAddr = playerCtx.InitialServerAddr ?? "";
            if (string.IsNullOrEmpty(currentServerAddr) && playerCtx.Servers != null && playerCtx.Servers.Count > 0)
                currentServerAddr = playerCtx.Servers[0].Addr;
        }
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
        txtQuery.TextChanged += delegate { OnQueryTextChanged(); };
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
        string cue;
        if (playerMode)
        {
            cue = "输入玩家名或拼音 | 留空看全部";
        }
        else
        {
            cue = CanPickPlayer
                ? "搜索指令 | 支持拼音搜索 | @选玩家ID"
                : "搜索指令 | 支持拼音搜索";
        }
        try { Native.SetCueBanner(txtQuery.Handle, cue); } catch { }
    }

    void ApplyModeTopControls()
    {
        if (segMode != null) segMode.Visible = !playerMode;
        if (IsSideMode)
        {
            txtQuery.SetBounds(40, 14, Width - 54, 24);
            if (!playerMode) segMode.SetBounds(12, 48, 116, 26);
            lblCount.SetBounds(Width - 62, 50, 50, 22);
            listPanel.SetBounds(6, 86, Width - 12, Height - 126);
        }
        else
        {
            txtQuery.SetBounds(42, 16, playerMode ? (Width - 282) : (Width - 232), 26);
            if (!playerMode) segMode.SetBounds(Width - 180, 13, 120, 28);
            lblCount.SetBounds(Width - 58, 16, 48, 22);
            listPanel.SetBounds(6, 56, Width - 12, Height - 92);
        }
    }

    void ApplyModeChrome()
    {
        ApplyModeTopControls();
        UpdateCueBanner();
        UpdateCountLabel();
        EnsureSelVisible();
        Invalidate();
        if (listPanel != null) listPanel.Invalidate();
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

        ApplyModeTopControls();
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
        if (suppressDeactivateClose) return;
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

    void OnQueryTextChanged()
    {
        if (suppressQueryEvent || txtQuery == null) return;
        string txt = txtQuery.Text ?? "";
        string pq;
        if (!playerMode)
        {
            if (CanPickPlayer && TryParseInlinePlayerQuery(txt, out pq))
            {
                EnterPlayerMode(true, pq);
                return;
            }
            RunSearch(txt);
            return;
        }

        if (playerModeByAt)
        {
            if (!TryParseInlinePlayerQuery(txt, out pq))
            {
                ExitPlayerMode(false, txt);
                return;
            }
            RebuildPlayerRows(pq);
        }
        else
        {
            RebuildPlayerRows(txt);
        }
    }

    void UpdateCountLabel()
    {
        if (lblCount == null) return;
        if (!playerMode)
        {
            lblCount.Text = matches.Count + " 项";
            return;
        }
        if (playerLoading)
        {
            lblCount.Text = "查询中…";
            return;
        }
        if (!string.IsNullOrEmpty(playerStatusNote) && currentRawPlayers.Count == 0)
        {
            lblCount.Text = "异常";
            return;
        }
        int cnt = 0;
        for (int i = 0; i < playerRows.Count; i++)
            if (!playerRows[i].IsResetRow) cnt++;
        lblCount.Text = cnt + " 人";
    }

    void RunSearch(string query)
    {
        string q = (query ?? "").Trim();
        curTokens = q.Length == 0 ? new string[0] : q.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        matches = ScoreAndFilter(allEntries, q, 120);
        selIdx = matches.Count > 0 ? 0 : -1;
        scrollIdx = 0;
        hoverIdx = -1;
        UpdateCountLabel();
        if (listPanel != null) listPanel.Invalidate();
    }

    void EnterPlayerMode(bool byAt, string initialFilter)
    {
        if (!CanPickPlayer) return;
        if (!byAt)
        {
            savedCmdQuery = (txtQuery != null) ? (txtQuery.Text ?? "") : "";
            if (txtQuery != null && txtQuery.TextLength > 0)
            {
                suppressQueryEvent = true;
                txtQuery.Text = "";
                suppressQueryEvent = false;
            }
        }
        else
        {
            savedCmdQuery = "";
        }

        playerMode = true;
        playerModeByAt = byAt;
        hoverIdx = -1;
        scrollIdx = 0;
        ApplyModeChrome();
        LoadPlayerCacheOrFetch(false, initialFilter ?? "");
        if (txtQuery != null && !txtQuery.IsDisposed) txtQuery.Focus();
    }

    void ExitPlayerMode(bool restoreSavedQuery, string overrideQuery)
    {
        if (!playerMode) return;
        playerQuerySeq++;
        playerLoading = false;
        playerStatusNote = "";
        playerMode = false;
        bool wasAt = playerModeByAt;
        playerModeByAt = false;
        hoverIdx = -1;
        serverPillHover = false;
        refreshPillHover = false;

        string nextQ;
        if (overrideQuery != null) nextQ = overrideQuery;
        else if (restoreSavedQuery && !wasAt) nextQ = savedCmdQuery ?? "";
        else nextQ = "";
        savedCmdQuery = "";

        if (txtQuery != null)
        {
            suppressQueryEvent = true;
            txtQuery.Text = nextQ;
            txtQuery.SelectionStart = txtQuery.TextLength;
            suppressQueryEvent = false;
        }

        ApplyModeChrome();
        RunSearch(nextQ);
        if (txtQuery != null && !txtQuery.IsDisposed) txtQuery.Focus();
    }

    string CurrentServerDisplayLabel()
    {
        if (playerCtx != null && playerCtx.Servers != null)
        {
            for (int i = 0; i < playerCtx.Servers.Count; i++)
            {
                if (string.Equals(playerCtx.Servers[i].Addr, currentServerAddr, StringComparison.OrdinalIgnoreCase))
                    return ShortServerLabel(playerCtx.Servers[i].Label);
            }
            if (playerCtx.Servers.Count > 0)
                return ShortServerLabel(playerCtx.Servers[0].Label);
        }
        return string.IsNullOrEmpty(currentServerAddr) ? "选择服务器" : ShortServerLabel(currentServerAddr);
    }

    void RebuildPlayerRows(string filterQuery)
    {
        curPlayerQuery = (filterQuery ?? "").Trim();
        bool hideBots = (playerCtx != null) ? playerCtx.HideBots : true;
        int sortMode = (playerCtx != null) ? playerCtx.SortMode : 0;
        List<A2SPlayerInfo> filtered = OnlinePlayerPickerForm.FilterAndSort(currentRawPlayers, curPlayerQuery, hideBots, sortMode);

        playerRows = new List<PalettePlayerRow>();
        if (!string.IsNullOrEmpty(currentTarget) && (curPlayerQuery.Length == 0 || curPlayerQuery == "^"))
            playerRows.Add(new PalettePlayerRow { IsResetRow = true, Player = null });

        for (int i = 0; i < filtered.Count; i++)
            playerRows.Add(new PalettePlayerRow { IsResetRow = false, Player = filtered[i] });

        selIdx = playerRows.Count > 0 ? 0 : -1;
        scrollIdx = 0;
        hoverIdx = -1;
        UpdateCountLabel();
        if (listPanel != null) listPanel.Invalidate();
    }

    void LoadPlayerCacheOrFetch(bool forceRefresh, string filterQuery)
    {
        if (!CanPickPlayer) return;
        string addr = currentServerAddr;
        if (string.IsNullOrEmpty(addr))
        {
            addr = playerCtx.Servers[0].Addr;
            currentServerAddr = addr;
        }

        OnlinePlayerCache cached;
        if (!forceRefresh && playerCtx.Cache != null && playerCtx.Cache.TryGetValue(addr, out cached)
            && cached != null && (DateTime.Now - cached.FetchedAt).TotalSeconds < 15.0)
        {
            currentRawPlayers = cached.Players ?? new List<A2SPlayerInfo>();
            playerLoading = false;
            playerStatusNote = "";
            RebuildPlayerRows(filterQuery);
            return;
        }

        int seq = ++playerQuerySeq;
        playerLoading = true;
        playerStatusNote = "正在获取玩家列表…";
        currentRawPlayers = new List<A2SPlayerInfo>();
        RebuildPlayerRows(filterQuery);
        Invalidate(RefreshPillRect);

        Task.Run(delegate
        {
            string host;
            int port;
            bool ok = false;
            int ping = -1;
            var players = new List<A2SPlayerInfo>();
            if (MainForm.ParseHostPort(addr, out host, out port))
                ok = MainForm.A2SPlayersQuery(host, port, 1800, out players, out ping);

            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed || !playerMode || seq != playerQuerySeq) return;
                    playerLoading = false;
                    if (!ok)
                    {
                        currentRawPlayers = new List<A2SPlayerInfo>();
                        playerStatusNote = "连不上服务器或已离线，按 F5 重试或切换服务器";
                        RebuildPlayerRows(CurrentPlayerQueryText());
                        Invalidate(RefreshPillRect);
                        return;
                    }
                    if (playerCtx.Cache != null)
                    {
                        playerCtx.Cache[addr] = new OnlinePlayerCache
                        {
                            Players = players,
                            PingMs = ping,
                            FetchedAt = DateTime.Now
                        };
                    }
                    currentRawPlayers = players;
                    playerStatusNote = players.Count == 0 ? "当前服务器没人在线" : "";
                    RebuildPlayerRows(CurrentPlayerQueryText());
                    Invalidate(RefreshPillRect);
                }));
            }
            catch { }
        });
    }

    string CurrentPlayerQueryText()
    {
        if (txtQuery == null) return "";
        string txt = txtQuery.Text ?? "";
        string pq;
        if (playerModeByAt && TryParseInlinePlayerQuery(txt, out pq)) return pq;
        return txt.Trim();
    }

    void LocateMyServerInPaletteAsync()
    {
        if (!CanPickPlayer) return;
        string target = (playerCtx.MySteamName ?? "").Trim();
        if (string.IsNullOrEmpty(target)) return;

        int seq = ++playerQuerySeq;
        playerLoading = true;
        playerStatusNote = "正在查找你所在的服务器…";
        currentRawPlayers = new List<A2SPlayerInfo>();
        RebuildPlayerRows(CurrentPlayerQueryText());
        Invalidate(RefreshPillRect);

        var snapshot = new List<OnlineServerEntry>(playerCtx.Servers);
        Task.Run(delegate
        {
            int n = snapshot.Count;
            var results = new OnlinePlayerCache[n];
            using (var gate = new SemaphoreSlim(8, 8))
            using (var done = new CountdownEvent(n))
            {
                for (int i = 0; i < n; i++)
                {
                    int ix = i;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            gate.Wait();
                            try
                            {
                                string host;
                                int port;
                                List<A2SPlayerInfo> plist;
                                int ping;
                                if (MainForm.ParseHostPort(snapshot[ix].Addr, out host, out port)
                                    && MainForm.A2SPlayersQuery(host, port, 1400, out plist, out ping))
                                {
                                    results[ix] = new OnlinePlayerCache
                                    {
                                        Players = plist,
                                        PingMs = ping,
                                        FetchedAt = DateTime.Now
                                    };
                                }
                            }
                            finally { try { gate.Release(); } catch { } }
                        }
                        catch { }
                        finally { try { done.Signal(); } catch { } }
                    });
                }
                done.Wait(4500);
            }

            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed || !playerMode || seq != playerQuerySeq) return;
                    playerLoading = false;
                    int exactIdx = -1;
                    int fuzzyIdx = -1;
                    for (int i = 0; i < n; i++)
                    {
                        OnlinePlayerCache oc = results[i];
                        if (oc == null) continue;
                        if (playerCtx.Cache != null) playerCtx.Cache[snapshot[i].Addr] = oc;
                        for (int j = 0; j < oc.Players.Count; j++)
                        {
                            string pn = (oc.Players[j].Name ?? "").Trim();
                            if (string.Equals(pn, target, StringComparison.OrdinalIgnoreCase))
                            {
                                if (exactIdx < 0) exactIdx = i;
                            }
                            else if (pn.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (fuzzyIdx < 0) fuzzyIdx = i;
                            }
                        }
                    }

                    int found = (exactIdx >= 0) ? exactIdx : fuzzyIdx;
                    if (found >= 0)
                    {
                        currentServerAddr = snapshot[found].Addr;
                        if (playerCtx.OnServerChanged != null) playerCtx.OnServerChanged(currentServerAddr);
                        currentRawPlayers = results[found].Players ?? new List<A2SPlayerInfo>();
                        playerStatusNote = "";
                        RebuildPlayerRows(CurrentPlayerQueryText());
                        Invalidate();
                    }
                    else
                    {
                        playerStatusNote = "没在在线服务器里找到 " + target;
                        UpdateCountLabel();
                        if (listPanel != null) listPanel.Invalidate();
                        Invalidate(RefreshPillRect);
                    }
                }));
            }
            catch { }
        });
    }

    void ApplyTargetAndReturn(string newTarget)
    {
        currentTarget = (newTarget ?? "").Trim();
        if (playerCtx != null && playerCtx.OnTargetChanged != null)
            playerCtx.OnTargetChanged(currentTarget);
        ExitPlayerMode(!playerModeByAt, playerModeByAt ? "" : null);
    }

    void CommitSelectedPlayer(bool copyOnly)
    {
        if (selIdx < 0 || selIdx >= playerRows.Count) return;
        PalettePlayerRow row = playerRows[selIdx];
        if (row.IsResetRow || row.Player == null)
        {
            if (!copyOnly) ApplyTargetAndReturn("");
            return;
        }
        string rawName = row.Player.Name ?? "";
        if (copyOnly)
        {
            menuTriggeredPaletteClose = true;
            Close();
            if (playerCtx != null && playerCtx.OnCopyPlayerName != null)
                playerCtx.OnCopyPlayerName(rawName, false);
            return;
        }
        ApplyTargetAndReturn(rawName);
    }

    bool IsSelfPlayer(string name)
    {
        if (playerCtx == null || string.IsNullOrEmpty(playerCtx.MySteamName) || string.IsNullOrEmpty(name)) return false;
        string s = playerCtx.MySteamName.Trim();
        if (s.Length == 0) return false;
        return string.Equals(name.Trim(), s, StringComparison.OrdinalIgnoreCase)
            || name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void CloseActiveMenu()
    {
        if (activePopupMenu != null)
        {
            try { activePopupMenu.Close(); } catch { }
            try { activePopupMenu.Dispose(); } catch { }
            activePopupMenu = null;
        }
    }

    ContextMenuStrip CreateThemedMenu()
    {
        CloseActiveMenu();
        var menu = new ContextMenuStrip();
        menu.ShowImageMargin = false;
        menu.BackColor = ThemePalette.SurfaceAlt;
        menu.ForeColor = ThemePalette.TextPrimary;
        menu.Font = fTitle;
        menu.Renderer = new ToolStripProfessionalRenderer(new PaletteMenuColorTable());
        menu.Closed += delegate
        {
            BeginInvoke((MethodInvoker)delegate
            {
                suppressDeactivateClose = false;
                if (!menuTriggeredPaletteClose && !IsDisposed && Visible)
                {
                    Native.MakeTopmost(Handle);
                    Activate();
                    if (txtQuery != null && !txtQuery.IsDisposed) txtQuery.Focus();
                }
            });
        };
        activePopupMenu = menu;
        return menu;
    }

    void ShowServerPickerMenu()
    {
        if (!CanPickPlayer) return;
        ContextMenuStrip menu = CreateThemedMenu();
        string myNm = (playerCtx.MySteamName ?? "").Trim();
        if (!string.IsNullOrEmpty(myNm))
        {
            var miLoc = new ToolStripMenuItem("查找我在哪个服（" + myNm + "）");
            miLoc.ForeColor = ThemePalette.AccentSoftFg;
            miLoc.Click += delegate { LocateMyServerInPaletteAsync(); };
            menu.Items.Add(miLoc);
            menu.Items.Add(new ToolStripSeparator());
        }
        for (int i = 0; i < playerCtx.Servers.Count; i++)
        {
            OnlineServerEntry srv = playerCtx.Servers[i];
            bool cur = string.Equals(srv.Addr, currentServerAddr, StringComparison.OrdinalIgnoreCase);
            var mi = new ToolStripMenuItem((cur ? "✓ " : "    ") + (srv.Label ?? srv.Addr));
            if (cur) mi.ForeColor = ThemePalette.AccentSoftFg;
            string addrCopy = srv.Addr;
            mi.Click += delegate
            {
                currentServerAddr = addrCopy;
                if (playerCtx.OnServerChanged != null) playerCtx.OnServerChanged(currentServerAddr);
                LoadPlayerCacheOrFetch(false, CurrentPlayerQueryText());
                Invalidate();
            };
            menu.Items.Add(mi);
        }
        menuTriggeredPaletteClose = false;
        suppressDeactivateClose = true;
        Rectangle rc = ServerPillRect;
        menu.Show(this, new Point(rc.Left, rc.Bottom + 3));
    }

    void ShowPlayerRowContextMenu(A2SPlayerInfo p, Point screenPt)
    {
        if (p == null) return;
        ContextMenuStrip menu = CreateThemedMenu();
        string nm = p.Name ?? "";

        var miTarget = new ToolStripMenuItem("设为当前目标");
        miTarget.Click += delegate { ApplyTargetAndReturn(nm); };
        menu.Items.Add(miTarget);

        var miCopy = new ToolStripMenuItem("复制昵称");
        miCopy.Click += delegate
        {
            menuTriggeredPaletteClose = true;
            Close();
            if (playerCtx != null && playerCtx.OnCopyPlayerName != null)
                playerCtx.OnCopyPlayerName(nm, false);
        };
        menu.Items.Add(miCopy);

        var miQuote = new ToolStripMenuItem("复制昵称（带引号）");
        miQuote.Click += delegate
        {
            menuTriggeredPaletteClose = true;
            Close();
            if (playerCtx != null && playerCtx.OnCopyPlayerName != null)
                playerCtx.OnCopyPlayerName(nm, true);
        };
        menu.Items.Add(miQuote);

        menuTriggeredPaletteClose = false;
        suppressDeactivateClose = true;
        menu.Show(screenPt);
    }

    int VisibleRows { get { return Math.Max(1, listPanel.ClientSize.Height / CurrentRowH); } }

    void EnsureSelVisible()
    {
        if (selIdx < 0) return;
        if (selIdx < scrollIdx) scrollIdx = selIdx;
        else if (selIdx >= scrollIdx + VisibleRows) scrollIdx = selIdx - VisibleRows + 1;
        int maxSc = Math.Max(0, CurrentItemCount - VisibleRows);
        if (scrollIdx > maxSc) scrollIdx = maxSc;
        if (scrollIdx < 0) scrollIdx = 0;
    }

    void ExecuteSelected(bool forceUlx)
    {
        if (playerMode)
        {
            CommitSelectedPlayer(forceUlx);
            return;
        }
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

        if (ctrl && key == Keys.T && CanPickPlayer)
        {
            if (playerMode) ExitPlayerMode(true, null);
            else EnterPlayerMode(false, "");
            return true;
        }
        if (key == Keys.Escape)
        {
            if (playerMode) ExitPlayerMode(!playerModeByAt, playerModeByAt ? "" : null);
            else Close();
            return true;
        }
        if (key == Keys.Down)
        {
            int cnt = CurrentItemCount;
            if (cnt > 0)
            {
                selIdx = (selIdx + 1) % cnt;
                EnsureSelVisible();
                listPanel.Invalidate();
            }
            return true;
        }
        if (key == Keys.Up)
        {
            int cnt = CurrentItemCount;
            if (cnt > 0)
            {
                selIdx = (selIdx <= 0) ? (cnt - 1) : (selIdx - 1);
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
        if (playerMode && key == Keys.F5)
        {
            LoadPlayerCacheOrFetch(true, CurrentPlayerQueryText());
            return true;
        }
        if (!playerMode && key == Keys.Tab)
        {
            ulxMode = !ulxMode;
            segMode.SetSelectedSilent(ulxMode ? 1 : 0);
            listPanel.Invalidate();
            return true;
        }
        if (!playerMode && ctrl && key == Keys.P)
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
        DrawFooterPill(g, font, rc, text, hovered, false);
    }

    static void DrawFooterPill(Graphics g, Font font, Rectangle rc, string text, bool hovered, bool active)
    {
        if (rc.IsEmpty || rc.Width < 4 || rc.Height < 4) return;
        Color fill = hovered ? ThemePalette.Hover : (active ? ThemePalette.AccentSoft : ThemePalette.CodeChipBg);
        Color border = (hovered || active) ? ThemePalette.Accent : ThemePalette.CodeChipEdge;
        Color fg = (hovered || active) ? ThemePalette.AccentSoftFg : ThemePalette.TextSecondary;
        using (var bg = new SolidBrush(fill))
            Native.FillRoundedRect(g, bg, rc, 5);
        using (var edge = new Pen(border, 1f))
            Native.DrawRoundedRect(g, edge, rc, 5);
        TextRenderer.DrawText(g, text, font, new Rectangle(rc.X + 4, rc.Y, Math.Max(4, rc.Width - 8), rc.Height),
            fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ThemePalette.Elevated);

        bool side = IsSideMode;
        string tgtText;
        if (CanPickPlayer)
            tgtText = string.IsNullOrEmpty(currentTarget) ? "目标：^ (自己) ▾" : ("目标：" + currentTarget + " ▾");
        else
            tgtText = string.IsNullOrEmpty(currentTarget) ? "目标：^ (自己)" : ("目标：" + currentTarget);

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

        if (playerMode)
        {
            DrawFooterPill(g, fFooter, ServerPillRect, CurrentServerDisplayLabel() + " ▾", serverPillHover, true);
            DrawFooterPill(g, fFooter, RefreshPillRect, playerLoading ? "…" : "↻", refreshPillHover, false);
        }

        bool tgtActive = playerMode || !string.IsNullOrEmpty(currentTarget);
        DrawFooterPill(g, fFooter, TargetPillRect, tgtText, targetPillHover, tgtActive);

        if (side)
        {
            string hint = playerMode
                ? "Enter 设目标 · Shift+Enter 复制名字 · Esc"
                : (CanPickPlayer ? "↑↓ 选择 · Enter 复制 · @ 选人 · Tab 切写法" : "↑↓ 选择 · Enter 复制 · Shift+Enter 控制台 · Tab");
            TextRenderer.DrawText(g, hint,
                fFooter, new Rectangle(12, Height - 34, Math.Max(60, Width - 182), 30), ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
        else
        {
            string hint = playerMode
                ? "↑↓ 选择 · Enter 设为目标 · Shift+Enter 复制名字 · F5 刷新 · Esc 返回"
                : (CanPickPlayer ? "↑↓ 选择 · Enter 复制 · Shift+Enter 控制台 · @ 选人 · Ctrl+P 收藏" : "↑↓ 选择 · Enter 复制 · Shift+Enter 控制台 · Ctrl+P 收藏 · Tab 切写法");
            TextRenderer.DrawText(g, hint,
                fFooter, new Rectangle(12, Height - 30, Math.Max(80, Width - 344), 26), ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        DrawFooterPill(g, fFooter, DockPillRect, "位置: " + DockModeLabel(dockMode), dockPillHover);
        string opTxt = (opacityPct >= 100) ? "不透明" : ("透明 " + opacityPct + "%");
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
        bool hitTgt = CanPickPlayer && TargetPillRect.Contains(e.Location);
        bool hitSrv = playerMode && ServerPillRect.Contains(e.Location);
        bool hitRef = playerMode && RefreshPillRect.Contains(e.Location);
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
        if (hitTgt != targetPillHover)
        {
            targetPillHover = hitTgt;
            Invalidate(TargetPillRect);
        }
        if (hitSrv != serverPillHover)
        {
            serverPillHover = hitSrv;
            Invalidate(ServerPillRect);
        }
        if (hitRef != refreshPillHover)
        {
            refreshPillHover = hitRef;
            Invalidate(RefreshPillRect);
        }
        Cursor = (hitDock || hitOp || hitTgt || hitSrv || hitRef) ? Cursors.Hand : Cursors.Default;
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
        if (targetPillHover)
        {
            targetPillHover = false;
            Invalidate(TargetPillRect);
        }
        if (serverPillHover)
        {
            serverPillHover = false;
            Invalidate(ServerPillRect);
        }
        if (refreshPillHover)
        {
            refreshPillHover = false;
            Invalidate(RefreshPillRect);
        }
        Cursor = Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            if (TargetPillRect.Contains(e.Location) && !string.IsNullOrEmpty(currentTarget))
            {
                currentTarget = "";
                if (playerCtx != null && playerCtx.OnTargetChanged != null)
                    playerCtx.OnTargetChanged("");
                if (playerMode) RebuildPlayerRows(CurrentPlayerQueryText());
                if (listPanel != null) listPanel.Invalidate();
                Invalidate(TargetPillRect);
            }
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        if (playerMode && ServerPillRect.Contains(e.Location))
        {
            ShowServerPickerMenu();
            return;
        }
        if (playerMode && RefreshPillRect.Contains(e.Location))
        {
            LoadPlayerCacheOrFetch(true, CurrentPlayerQueryText());
            if (txtQuery != null && !txtQuery.IsDisposed) txtQuery.Focus();
            return;
        }
        if (CanPickPlayer && TargetPillRect.Contains(e.Location))
        {
            if (playerMode) ExitPlayerMode(!playerModeByAt, playerModeByAt ? "" : null);
            else EnterPlayerMode(false, "");
            return;
        }
        if (DockPillRect.Contains(e.Location))
        {
            dockMode = NextDockMode(dockMode);
            dockPillHover = false;
            opacityPillHover = false;
            targetPillHover = false;
            serverPillHover = false;
            refreshPillHover = false;
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

    void OnPlayerListPaint(Graphics g, int w, int h)
    {
        if (playerRows.Count == 0)
        {
            string msg = playerLoading
                ? "正在获取玩家列表…"
                : (!string.IsNullOrEmpty(playerStatusNote) ? playerStatusNote : "没找到在线玩家，换个词或清空看全部");
            TextRenderer.DrawText(g, msg, fTitle,
                new Rectangle(20, 40, Math.Max(40, w - 40), 40), ThemePalette.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
            return;
        }

        int rh = CurrentRowH;
        int vis = VisibleRows + 1;
        int rankBase = 0;
        for (int k = 0; k < scrollIdx && k < playerRows.Count; k++)
            if (!playerRows[k].IsResetRow) rankBase++;

        for (int vi = 0; vi < vis; vi++)
        {
            int idx = scrollIdx + vi;
            if (idx >= playerRows.Count) break;
            int y = vi * rh;
            if (y >= h) break;

            PalettePlayerRow row = playerRows[idx];
            bool sel = (idx == selIdx);
            bool hov = (!sel && idx == hoverIdx);
            Rectangle rowRc = new Rectangle(6, y + 2, w - 12, rh - 4);

            if (sel)
            {
                using (var sb = new SolidBrush(ThemePalette.Selected))
                    Native.FillRoundedRect(g, sb, rowRc, 7);
                using (var ab = new SolidBrush(ThemePalette.Accent))
                    Native.FillRoundedRect(g, ab, new Rectangle(rowRc.X + 2, rowRc.Y + 7, 3, rowRc.Height - 14), 2);
            }
            else if (hov)
            {
                using (var hb = new SolidBrush(ThemePalette.Hover))
                    Native.FillRoundedRect(g, hb, rowRc, 7);
            }

            if (row.IsResetRow || row.Player == null)
            {
                var resetTag = new Rectangle(rowRc.X + 10, rowRc.Y + (rowRc.Height - 20) / 2, 26, 20);
                using (var rb = new SolidBrush(ThemePalette.CodeChipBg))
                    Native.FillRoundedRect(g, rb, resetTag, 5);
                TextRenderer.DrawText(g, "^", fCode, resetTag, ThemePalette.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                int rightW = 140;
                TextRenderer.DrawText(g, "当前：" + currentTarget, fFooter,
                    new Rectangle(rowRc.Right - rightW - 10, rowRc.Y, rightW, rowRc.Height), ThemePalette.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                TextRenderer.DrawText(g, "重置为自己（^）", fTitle,
                    new Rectangle(resetTag.Right + 10, rowRc.Y, Math.Max(60, rowRc.Width - rightW - 60), rowRc.Height),
                    sel ? ThemePalette.AccentSoftFg : ThemePalette.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                continue;
            }

            rankBase++;
            A2SPlayerInfo p = row.Player;
            string pName = p.Name ?? "";
            bool isCur = !string.IsNullOrEmpty(currentTarget) && string.Equals(pName.Trim(), currentTarget.Trim(), StringComparison.OrdinalIgnoreCase);
            bool isMe = IsSelfPlayer(pName);

            Rectangle rankRc = new Rectangle(rowRc.X + 10, rowRc.Y, 30, rowRc.Height);
            TextRenderer.DrawText(g, isCur ? "✓" : ("#" + rankBase), isCur ? fTitle : fFooter, rankRc,
                isCur ? ThemePalette.Accent : ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            int nameLeft = rankRc.Right + 2;
            if (isMe)
            {
                Rectangle meRc = new Rectangle(nameLeft, rowRc.Y + (rowRc.Height - 18) / 2, 22, 18);
                using (var mb = new SolidBrush(ThemePalette.Accent))
                    Native.FillRoundedRect(g, mb, meRc, 4);
                TextRenderer.DrawText(g, "我", fBadge, meRc, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                nameLeft += 27;
            }
            else if (p.IsBot)
            {
                Rectangle botRc = new Rectangle(nameLeft, rowRc.Y + (rowRc.Height - 18) / 2, 30, 18);
                using (var bb = new SolidBrush(ThemePalette.CodeChipBg))
                    Native.FillRoundedRect(g, bb, botRc, 4);
                TextRenderer.DrawText(g, "BOT", fBadge, botRc, ThemePalette.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                nameLeft += 35;
            }

            int durW = 48;
            int scoreW = 54;
            Rectangle durRc = new Rectangle(rowRc.Right - durW - 10, rowRc.Y, durW, rowRc.Height);
            Rectangle scoreRc = new Rectangle(durRc.Left - scoreW - 6, rowRc.Y, scoreW, rowRc.Height);
            Color durCol = (p.DurationSec < 60 && !p.IsBot) ? ThemePalette.Success : ThemePalette.TextMuted;
            TextRenderer.DrawText(g, A2SPlayerInfo.FormatDuration(p.DurationSec), fFooter, durRc, durCol,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, p.Score + " 分", fFooter, scoreRc, ThemePalette.TextSecondary,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            int nameW = Math.Max(40, scoreRc.Left - nameLeft - 8);
            TextRenderer.DrawText(g, pName, fTitle, new Rectangle(nameLeft, rowRc.Y, nameW, rowRc.Height),
                (sel || isCur) ? ThemePalette.AccentSoftFg : ThemePalette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    void OnListPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ThemePalette.Surface);
        int w = listPanel.ClientSize.Width;
        int h = listPanel.ClientSize.Height;

        if (playerMode)
        {
            OnPlayerListPaint(g, w, h);
            return;
        }

        if (matches.Count == 0)
        {
            TextRenderer.DrawText(g, "没找到匹配的指令，换个词或拼音试试", fTitle,
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
        if (idx < 0 || idx >= CurrentItemCount) idx = -1;
        if (idx != hoverIdx)
        {
            hoverIdx = idx;
            listPanel.Cursor = (idx >= 0) ? Cursors.Hand : Cursors.Default;
            listPanel.Invalidate();
        }
    }

    void OnListMouseDown(object sender, MouseEventArgs e)
    {
        int rh = CurrentRowH;
        int idx = scrollIdx + (e.Y / rh);
        if (idx < 0 || idx >= CurrentItemCount) return;

        if (playerMode)
        {
            selIdx = idx;
            listPanel.Invalidate();
            if (e.Button == MouseButtons.Right)
            {
                PalettePlayerRow row = playerRows[idx];
                if (!row.IsResetRow && row.Player != null)
                    ShowPlayerRowContextMenu(row.Player, listPanel.PointToScreen(e.Location));
                return;
            }
            if (e.Button == MouseButtons.Left)
                CommitSelectedPlayer((Control.ModifierKeys & Keys.Shift) != 0);
            return;
        }

        if (e.Button != MouseButtons.Left) return;
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
        int maxSc = Math.Max(0, CurrentItemCount - VisibleRows);
        scrollIdx = Math.Max(0, Math.Min(maxSc, scrollIdx + delta));
        listPanel.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseActiveMenu();
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
