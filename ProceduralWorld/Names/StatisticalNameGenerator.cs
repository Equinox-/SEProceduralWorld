using System;
using System.Collections.Generic;
using System.Text;
using Equinox.Utils.Session;
using Equinox.Utils.Stream;
using VRage.Utils;

namespace Equinox.ProceduralWorld.Names
{
    public class StatisticalNameGenerator : NameGeneratorBase
    {
        #region DictionaryPacking
        // ReSharper disable once SuggestBaseTypeForParameter
        private static void Swap(byte[] data, int a)
        {
            var f0 = data[a];
            var f1 = data[a + 1];
            data[a] = data[a + 3];
            data[a + 1] = data[a + 2];
            data[a + 2] = f1;
            data[a + 3] = f0;
        }
        public static List<KeyValuePair<string, float>> UnpackDictionary(string arra)
        {
            var data = Convert.FromBase64String(arra);
            var i = 0;
            if (!BitConverter.IsLittleEndian)
                Swap(data, i);
            var entries = BitConverter.ToInt32(data, i);
            i += 4;
            var result = new List<KeyValuePair<string, float>>(entries);
            for (var j = 0; j < entries; j++)
            {
                var slen = data[i] & 0xFF;
                i++;
                var name = Encoding.UTF8.GetString(data, i, slen);
                i += slen;
                if (!BitConverter.IsLittleEndian)
                    Swap(data, i);
                var weight = BitConverter.ToSingle(data, i);
                i += 4;
                result.Add(new KeyValuePair<string, float>(name, weight));
            }
            return result;
        }

        public static string PackDictionary(List<KeyValuePair<string, float>> database)
        {
            using (var stream = MemoryStream.CreateEmptyStream(database.Count * 10))
            {
                stream.Write(database.Count);
                if (!BitConverter.IsLittleEndian)
                    Swap(stream.Backing, stream.WriteHead - 4);
                foreach (var pair in database)
                {
                    var bytes = Encoding.UTF8.GetBytes(pair.Key);
                    stream.Write((byte)bytes.Length);
                    stream.Write(bytes);
                    stream.Write(BitConverter.GetBytes(pair.Value));
                    if (!BitConverter.IsLittleEndian)
                        Swap(stream.Backing, stream.WriteHead - 4);
                }
                return Convert.ToBase64String(stream.Backing, 0, stream.WriteHead);
            }
        }

        private static readonly List<KeyValuePair<string, float>> EnglishNames = UnpackDictionary("6AMAAAVKYW1lc/SGqDwESm9obsBpJz0GUm9iZXJ0bTh3PQdNaWNoYWVssA6dPQRNYXJ5M3S+PQdXaWxsaWFt1JjdPQVEYXZpZMeZ+z0HUmljaGFyZKCcCD4HQ2hhcmxlc+tLEj4GSm9zZXBowjgbPgZUaG9tYXN3+yM+CFBhdHJpY2lhUM4qPgtDaHJpc3RvcGhlclhgMT4FTGluZGEi7zc+B0JhcmJhcmEtIz4+BkRhbmllbDhXRD4EUGF1bORdSj4ETWFya1xUUD4JRWxpemFiZXRoWERWPgZEb25hbGTZLVw+CEplbm5pZmVyWhdiPgZHZW9yZ2XbAGg+BU1hcmlhUEhtPgdLZW5uZXRoDIZyPgVTdXNhbu6Pdz4GU3RldmVuXYZ8PgZFZHdhcmTHvIA+CE1hcmdhcmV0pyyDPgVCcmlhbpqChT4GUm9uYWxkctCHPgdEb3JvdGh5Sh6KPgdBbnRob2555WiMPgRMaXNhS6OOPgVLZXZpbmPFkD4FTmFuY3k95JI+BUthcmVueAGVPgVCZXR0ebMelz4FSGVsZW6xOJk+BUphc29ur1KbPgdNYXR0aGV3b2mdPgRHYXJ5UnufPgdUaW1vdGh5fIOhPgZTYW5kcmHtgaM+BEpvc2UHdaU+BUxhcnJ5KlunPgdKZWZmcmV50TqpPgVGcmFua/0Tqz4FRG9ubmEp7aw+BUNhcm9snLyuPgRSdXRolIWwPgVTY290dDRDsj4ERXJpYzX/sz4HU3RlcGhlbvi3tT4GQW5kcmV3fm23PgZTaGFyb24NFrk+CE1pY2hlbGxlXru6PgVMYXVyYTRavD4FU2FyYWhr970+CEtpbWJlcmx5ZZG/PgdEZWJvcmFopiHBPgdKZXNzaWNhqa7CPgdSYXltb25krDvEPgdTaGlybGV5EMfFPgdDeW50aGlh3kPHPgZBbmdlbGGswMg+B01lbGlzc2GeOMo+BkJyZW5kYRWqyz4DQW15ThjNPgVKZXJyeQyAzj4HR3JlZ29yecrnzz4EQW5uYelN0T4GSm9zaHVhK6/SPghWaXJnaW5pYZEL1D4HUmViZWNjYfdn1T4IS2F0aGxlZW6Av9Y+BkRlbm5pcy0S2D4GUGFtZWxhO2PZPgZNYXJ0aGELsdo+BURlYnJhnvvbPgZBbWFuZGGSRN0+BldhbHRlckiK3j4JU3RlcGhhbmll/s/fPgZXaWxsaWUVFOE+B1BhdHJpY2tQU+I+BVRlcnJ57JDjPgdDYXJvbHluq8nkPgVQZXRlcswA5j4JQ2hyaXN0aW5l7TfnPgVNYXJpZdBr6D4FSmFuZXSzn+k+B0ZyYW5jZXNY0Oo+CUNhdGhlcmluZV7/6z4GSGFyb2xkxiztPgVIZW5yefBW7j4HRG91Z2xhcxqB7z4FSm95Y2UGqPA+A0FubvLO8T4FRGlhbmWg8vI+BUFsaWNlsBT0PgRKZWFuRDD1PgVKdWxpZTlK9j4EQ2FybC5k9z4FS2VsbHmFfPg+B0hlYXRoZXJgjvk+BkFydGh1cjug+j4GVGVyZXNhFrL7PgZHbG9yaWFSwvw+BURvcmlzjtL9PgRSeWFuyuL+PgNKb2WL7P8+BVJvZ2Vyh3kAPwZFdmVseW7J/AA/BEp1YW5sfgE/BkFzaGxleT//AT8ESmFjaxKAAj8GQ2hlcnlsFgADPwZBbGJlcnQagAM/BEpvYW4eAAQ/B01pbGRyZWRTfwQ/CUthdGhlcmluZYj+BD8GSnVzdGluvX0FPwhKb25hdGhhbvL8BT8GR2VyYWxkiHoGPwVLZWl0aB74Bj8GU2FtdWVs5HQHPwZKdWRpdGid7Qc/BFJvc2WHZQg/BkphbmljZWTZCD8ITGF3cmVuY2VxTAk/BVJhbHBor74JPwZOaWNvbGXtMAo/BEp1ZHmMoQo/CE5pY2hvbGFzXBELPwlDaHJpc3RpbmEsgQs/A1JvefzwCz8FS2F0aHn8Xww/B1RoZXJlc2Etzgw/CEJlbmphbWluXjwNPwdCZXZlcmx58KgNPwZEZW5pc2WzFA4/BUJydWNlpn8OPwdCcmFuZG9umeoOPwRBZGFtvVQPPwVUYW1teRG+Dz8FSXJlbmX3JBA/BEZyZWTdixA/BUJpbGx5w/IQPwVIYXJyedpYET8ESmFuZfG+ET8FV2F5bmU4JBI/BUxvdWlzsIgSPwRMb3JpKO0SPwVTdGV2ZdBQEz8FVHJhY3l4tBM/BkplcmVteVEXFD8GUmFjaGVsW3kUPwZBbmRyZWFl2xQ/BUFhcm9ubz0VPwdNYXJpbHlueZ8VPwVSb2JpbrMAFj8FUmFuZHlOYBY/Bkxlc2xpZem/Fj8HS2F0aHJ5boQfFz8GRXVnZW5lUH4XPwVCb2JieUzcFz8GSG93YXJkSDoYPwZDYXJsb3NEmBg/BFNhcmFx9Rg/BkxvdWlzZZ5SGT8KSmFjcXVlbGluZcuvGT8EQW5uZfgMGj8FV2FuZGFWaRo/B1J1c3NlbGzkxBo/BVNoYXduciAbPwZWaWN0b3Ixexs/BUp1bGlh8NUbPwZCb25uaWWvMBw/BFJ1Ynmeihw/BUNocmlzjeQcPwRUaW5hfD4dPwRMb2lza5gdPwdQaHlsbGlzi/EdPwVKYW1pZatKHj8FTm9ybWHLox4/Bk1hcnRpbuv8Hj8FUGF1bGE8VR8/BUplc3Nlja0fPwVEaWFuYQ4FID8FQW5uaWWPXCA/B1NoYW5ub24QtCA/BkVybmVzdJELIT8EVG9kZENiIT8HUGhpbGxpcPW4IT8DTGVlpw8iPwdMaWxsaWFuiWUiPwVQZWdnecy5Ij8FRW1pbHkPDiM/B0NyeXN0YWxSYiM/A0tpbca1Iz8FQ3JhaWc6CSQ/BkNhcm1lbq5cJD8GR2xhZHlzIrAkPwZDb25uaWWWAyU/BFJpdGE7ViU/BEFsYW7gqCU/BERhd26F+yU/CEZsb3JlbmNlWk0mPwREYWxlL58mPwRTZWFuNfAmPwdGcmFuY2lzO0EnPwZKb2hubnlBkic/CENsYXJlbmNlR+MnPwZQaGlsaXB9Myg/BEVkbmGzgyg/B1RpZmZhbnka0yg/BFRvbnmBIik/BFJvc2HocSk/BUppbW15gMApPwRFYXJsGA8qPwVDaW5kebBdKj8HQW50b25pb0isKj8ETHVpcxD6Kj8ETWlrZdhHKz8FRGFubnmglSs/BUJyeWFuaOMrPwVHcmFjZWEwLD8HU3Rhbmxlebt7LD8HTGVvbmFyZBXHLD8FV2VuZHlvEi0/Bk5hdGhhbsldLT8GTWFudWVshKctPwZDdXJ0aXM/8S0/CFZpY3RvcmlhKzouPwZSb2RuZXkXgy4/Bk5vcm1hbgPMLj8FRWRpdGjvFC8/BlNoZXJyeQtdLz8GU3lsdmlhJ6UvPwlKb3NlcGhpbmVD7S8/BUFsbGVukDQwPwZUaGVsbWHdezA/BlNoZWlsYSrDMD8FRXRoZWynCTE/CE1hcmpvcmllJFAxPwRMeW5uoZYxPwVFbGxlbh7dMT8GRWxhaW5lmyMyPwZNYXJ2aW5JaTI/BkNhcnJpZfeuMj8GTWFyaW9upfQyPwlDaGFybG90dGWEOTM/B1ZpbmNlbnRjfjM/BUdsZW5uQsMzPwZUcmF2aXNRBzQ/Bk1vbmljYWBLND8HSmVmZmVyeW+PND8ESmVmZn7TND8GRXN0aGVyjRc1PwdQYXVsaW5lzVo1PwVKYWNvYg2eNT8ERW1tYU3hNT8EQ2hhZI0kNj8ES3lsZc1nNj8HSnVhbml0YQ2rNj8ERGFuYU3uNj8GTWVsdmluvTA3PwZKZXNzaWUtczc/BlJob25kYZ21Nz8FQW5pdGEN+Dc/BkFsZnJlZH06OD8FSGF6ZWwefDg/BUFtYmVyv704PwNFdmGQ/jg/B0JyYWRsZXlhPzk/A1JheTKAOT8FSmVzdXM0wDk/BkRlYmJpZTYAOj8HSGVyYmVydGk/Oj8FRWRkaWWcfjo/BEpvZWz/vDo/CUZyZWRlcmlja2L7Oj8FQXByaWzFOTs/B0x1Y2lsbGUoeDs/BUNsYXJhi7Y7PwRHYWlsH/Q7PwZKb2FubmXjMDw/B0VsZWFub3KnbTw/B1ZhbGVyaWVrqjw/CERhbmllbGxlL+c8PwRFcmluJCM9PwVFZHdpbhlfPT8FTWVnYW4Omz0/BkFsaWNpYTTWPT8HU3V6YW5uZVoRPj8HTWljaGVsZYBMPj8DRG9upoc+PwZCZXJ0aGH8wT4/CFZlcm9uaWNhg/s+PwRKaWxsCjU/PwdEYXJsZW5lkW4/PwVSaWNreRioPz8GTGF1cmVun+E/PwlHZXJhbGRpbmUmG0A/BFRyb3ndU0A/BVN0YWN5lIxAPwdSYW5kYWxsS8VAPwVDYXRoeTP9QD8FSm9hbm4bNUE/BVNhbGx5M2xBPwhMb3JyYWluZUujQT8FQmFycnlj2kE/CUFsZXhhbmRlcnsRQj8GUmVnaW5hxEdCPwZKYWNraWUNfkI/BUVyaWNhh7NCPwhCZWF0cmljZQHpQj8HRG9sb3Jlc6sdQz8HQmVybmljZVVSQz8FTWFyaW8whkM/B0Jlcm5hcmQLukM/BkF1ZHJleebtQz8GWXZvbm5lwSFEPwlGcmFuY2lzY2+cVUQ/B01pY2hlYWyniEQ/BUxlcm95srtEPwRKdW5lve5EPwdBbm5ldHRlyCFFPwhTYW1hbnRoYQRURT8GTWFyY3VzQIZFPwhUaGVvZG9yZXy4RT8FT3NjYXK46kU/CENsaWZmb3Jk9BxGPwZNaWd1ZWxhTkY/A0phec5/Rj8FUmVuZWVrsEY/A0FuYQjhRj8GVml2aWFu1hBHPwNKaW2kQEc/A0lkYXJwRz8DVG9tQKBHPwZSb25uaWUO0Ec/B1JvYmVydGHc/0c/BUhvbGx5qi9IPwhCcml0dGFueXhfSD8FQW5nZWxGj0g/BEFsZXgUv0g/B01lbGFuaWXi7kg/A0pvbrAeST8HWW9sYW5kYa5NST8FVG9tbXmsfEk/B0xvcmV0dGGqq0k/CEplYW5ldHRlqNpJPwZDYWx2aW6mCUo/BkxhdXJpZaQ4Sj8ETGVvbtNmSj8FS2F0aWUClUo/BlN0YWNleTHDSj8FTGxveWRg8Uo/BURlcmVrjx9LPwRCaWxsvk1LPwdWYW5lc3NhHXtLPwNTdWV8qEs/B0tyaXN0ZW7b1Us/BEFsbWE6A0w/BldhcnJlbpkwTD8FRWxzaWX4XUw/BEJldGhXi0w/BVZpY2tp57dMPwZKZWFubmV35Ew/Bkplcm9tZTgQTT8HRGFycmVsbPk7TT8EVGFyYbpnTT8IUm9zZW1hcnl7k00/A0xlbzy/TT8FRmxveWT96k0/BERlYW6+Fk4/BUNhcmxhf0JOPwZXZXNsZXlwbU4/BVRlcnJpYZhOPwZFaWxlZW5Sw04/CENvdXJ0bmV5Q+5OPwVBbHZpbjQZTz8DVGltVkNPPwVKb3JnZXhtTz8ER3JlZ5qXTz8GR29yZG9uvMFPPwVQZWRyb97rTz8ETHVjeQAWUD8IR2VydHJ1ZGUiQFA/BkR1c3RpbkRqUD8HRGVycmlja2aUUD8FQ29yZXmIvlA/BVRvbnlh2udQPwNEYW4sEVE/BEVsbGF+OlE/BUxld2lz0GNRPwdaYWNoYXJ5U4xRPwVXaWxtYda0UT8HTWF1cmljZVndUT8HS3Jpc3RpbtwFUj8ER2luYV8uUj8GVmVybm9u4lZSPwRWZXJhZX9SPwdSb2JlcnRv6KdSPwdOYXRhbGlla9BSPwVDbHlkZe74Uj8FQWduZXNxIVM/Bkhlcm1hbiRJUz8IQ2hhcmxlbmXXcFM/B0NoYXJsaWWKmFM/BkJlc3NpZT3AUz8FU2hhbmUh51M/B0RlbG9yZXMFDlQ/A1Nhbek0VD8FUGVhcmzNW1Q/B01lbGluZGGxglQ/BkhlY3RvcpWpVD8ER2xlbnnQVD8GQXJsZW5lXfdUPwdSaWNhcmRvch1VPwZUYW1hcmG3QlU/B01hdXJlZW78Z1U/Bkxlc3RlckGNVT8ER2VuZYayVT8HQ29sbGVlbsvXVT8HQWxsaXNvbhD9VT8FVHlsZXJVIlY/BFJpY2uaR1Y/A0pved9sVj8HSm9obm5pZSSSVj8HR2VvcmdpYWm3Vj8JQ29uc3RhbmNlrtxWPwVSYW1vbiQBVz8GTWFyY2lhmiVXPwZMaWxsaWUQSlc/B0NsYXVkaWGGblc/BUJyZW50/JJXPwVUYW55YXK3Vz8GTmVsbGll6NtXPwZNaW5uaWVeAFg/B0dpbGJlcnTUJFg/B01hcmxlbmV6SFg/BUhlaWRpIGxYPwZHbGVuZGHGj1g/BE1hcmNss1g/BVZpb2xhQ9ZYPwZNYXJpYW4a+Vg/BUx5ZGlh8RtZPwZCaWxsaWXIPlk/BlN0ZWxsYZ9hWT8JR3VhZGFsdXBldoRZPwhDYXJvbGluZU2nWT8IUmVnaW5hbGQkylk/BERvcmH77Fk/AkpvAw9aPwVDZWNpbAsxWj8FQ2FzZXkTU1o/BUJyZXR0G3VaPwZWaWNraWUjl1o/BVJ1YmVuK7laPwVKYWltZTPbWj8GUmFmYWVsa/xaPwlOYXRoYW5pZWyjHVs/Bk1hdHRpZds+Wz8GTWlsdG9uE2BbPwVFZGdhckuBWz8EUmF1bLShWz8GTWF4aW5lHcJbPwRJcm1hhuJbPwZNeXJ0bGXvAlw/Bk1hcnNoYVgjXD8FTWFiZWzBQ1w/B0NoZXN0ZXIqZFw/A0JlbpOEXD8FQW5kcmX8pFw/BkFkcmlhbmXFXD8ETGVuYf7kXD8IRnJhbmtsaW6XBF0/BUR1YW5lMCRdPwdDaHJpc3R5yUNdPwZUcmFjZXmTYl0/BVBhdHN5XYFdPwdHYWJyaWVsJ6BdPwZEZWFubmHxvl0/BkppbW1pZbvdXT8FSGlsZGGF/F0/BUVsbWVyTxtePwlDaHJpc3RpYW4ZOl4/BkJvYmJpZeNYXj8JR3dlbmRvbHlu3XZePwROb3Jh15RePwhNaXRjaGVsbNGyXj8GSmVubmlly9BePwRCcmFkxe5ePwNSb27wC18/BlJvbGFuZBspXz8ETmluYUZGXz8GTWFyZ2llcWNfPwRMZWFonIBfPwZIYXJ2ZXnHnV8/BENvcnnyul8/CUNhc3NhbmRyYR3YXz8GQXJub2xkSPVfPwlQcmlzY2lsbGFzEmA/BVBlbm55ni9gPwVOYW9taclMYD8DS2F59GlgPwRLYXJsH4dgPwVKYXJlZEqkYD8GQ2Fyb2xldcFgPwRPbGdhoN5gPwNKYW7L+2A/BkJyYW5kefYYYT8GTG9ubmllUjVhPwVMZW9uYa5RYT8GRGlhbm5lCm5hPwZDbGF1ZGVmimE/BVNvbmlhwqZhPwZKb3JkYW4ew2E/BUplbm55et9hPwdGZWxpY2lh1vthPwRFcmlrMhhiPwdMaW5kc2V5vjNiPwVLZXJyeUpPYj8GRGFycnls1mpiPwVWZWxtYWKGYj8ETmVpbO6hYj8GTWlyaWFter1iPwVCZWNreQbZYj8GVmlvbGV0w/NiPwhLcmlzdGluYYAOYz8GSmF2aWVyPSljPwhGZXJuYW5kb/pDYz8EQ29kebdeYz8HQ2xpbnRvbnR5Yz8GVHlyb25lMZRjPwRUb25p7q5jPwNUZWSryWM/BFJlbmVo5GM/Bk1hdGhldyX/Yz8HTGluZHNheeIZZD8FSnVsaW+fNGQ/BkRhcnJlblxPZD8FTWlzdHlJaWQ/A01hZTaDZD8FTGFuY2UjnWQ/BlNoZXJyaRC3ZD8GU2hlbGx5/dBkPwVTYW5keerqZD8GUmFtb25h1wRlPwNQYXTEHmU/BEt1cnSxOGU/BEpvZHmeUmU/BURhaXN5i2xlPwZOZWxzb26phWU/B0thdHJpbmHHnmU/BUVyaWth5bdlPwZDbGFpcmUD0WU/BUFsbGFuIeplPwRIdWdocAJmPwNHdXm/GmY/B0NsYXl0b24OM2Y/BlNoZXJ5bF1LZj8DTWF4rGNmPwlNYXJnYXJpdGH7e2Y/BkdlbmV2YUqUZj8GRHdheW5lmaxmPwdCZWxpbmRh6MRmPwVGZWxpeDfdZj8ERmF5ZYb1Zj8GRHdpZ2h01Q1nPwRDb3JhJCZnPwdBcm1hbmRvcz5nPwdTYWJyaW5h8lVnPwdOYXRhc2hhcW1nPwZJc2FiZWzwhGc/B0V2ZXJldHRvnGc/A0FkYe6zZz8HV2FsbGFjZW3LZz8GU2lkbmV57OJnPwpNYXJndWVyaXRla/pnPwNJYW7qEWg/BkhhdHRpZWkpaD8HSGFycmlldOhAaD8FUm9zaWWYV2g/BU1vbGx5SG5oPwZLcmlzdGn4hGg/A0tlbqibaD8GSm9hbm5hWLJoPwRJcmlzCMloPwdDZWNpbGlhuN9oPwZCcmFuZGlo9mg/A0JvYhgNaT8HQmxhbmNoZcgjaT8GSnVsaWFueDppPwZFdW5pY2UoUWk/BUFuZ2ll2GdpPwdBbGZyZWRviH5pPwVMeW5kYWiUaT8ESXZhbkiqaT8ESW5leijAaT8HRnJlZGRpZQjWaT8ERGF2ZejraT8HQWxiZXJ0b8gBaj8ITWFkZWxpbmXZFmo/BURhcnls6itqPwVCeXJvbvtAaj8GQW1lbGlhDFZqPwdBbGJlcnRhHWtqPwVTb255YS6Aaj8FUGVycnk/lWo/Bk1vcnJpc1Cqaj8HTW9uaXF1ZWG/aj8GTWFnZ2llctRqPwhLcmlzdGluZYPpaj8FS2F5bGGU/mo/BEpvZGmlE2s/BUphbmlltihrPwVJc2FhY8c9az8JR2VuZXZpZXZl2FJrPwdDYW5kYWNl6WdrPwZZdmV0dGX6fGs/B1dpbGxhcmQLkms/B1doaXRuZXkcp2s/BlZpcmdpbC28az8EUm9zcz7Raz8ET3BhbE/maz8GTWVsb2R5YPtrPwdNYXJ5YW5ucRBsPwhNYXJzaGFsbIIlbD8GRmFubmllkzpsPwdDbGlmdG9upE9sPwZBbGlzb261ZGw/BVN1c2ll9nhsPwdTaGVsbGV5N41sPwZTZXJnaW94oWw/CFNhbHZhZG9yubVsPwZPbGl2aWH6yWw/A0x1ejvebD8ES2lya3zybD8FRmxvcmG9Bm0/BEFuZHn+Gm0/BVZlcm5hPy9tPwhUZXJyYW5jZYBDbT8EU2V0aMFXbT8FTWFtaWUCbG0/BEx1bGFDgG0/BExvbGGElG0/BktyaXN0ecWobT8ES2VudAa9bT8GQmV1bGFoR9FtPwpBbnRvaW5ldHRliOVtPwhUZXJyZW5jZfr4bT8FR2F5bGVsDG4/B0VkdWFyZG/eH24/A1BhbYEybj8FS2VsbGkkRW4/BUp1YW5hx1duPwRKb2V5ampuPwlKZWFubmV0dGUNfW4/B0VucmlxdWWwj24/BkRvbm5pZVOibj8HQ2FuZGljZfa0bj8EV2FkZZnHbj8GSGFubmFoPNpuPwdGcmFua2ll3+xuPwdCcmlkZ2V0gv9uPwZBdXN0aW4lEm8/BlN0dWFydPgjbz8FS2FybGHLNW8/BEV2YW6eR28/BUNlbGlhcVlvPwVWaWNreURrbz8GU2hlbGlhF31vPwVQYXR0eeqObz8ETmlja72gbz8FTHlubmWQsm8/Bkx1dGhlcmPEbz8GTGF0b3lhNtZvPwhGcmVkcmljawnobz8FRGVsbGHc+W8/BkFydHVyb68LcD8JQWxlamFuZHJvgh1wPwdXZW5kZWxsVS9wPwVTaGVyaShBcD8ITWFyaWFubmX7UnA/Bkp1bGl1c85kcD8ISmVyZW1pYWihdnA/BVNoYXVupYdwPwRPdGlzqZhwPwRLYXJhralwPwlKYWNxdWVseW6xunA/BEVybWG1y3A/BkJsYW5jYbnccD8GQW5nZWxvve1wPwZBbGV4aXPB/nA/BlRyZXZvcsUPcT8HUm94YW5uZckgcT8GT2xpdmVyzTFxPwRNeXJh0UJxPwZNb3JnYW7VU3E/BEx1a2XZZHE/B0xldGljaWHddXE/BktyaXN0YeGGcT8FSG9tZXLll3E/BkdlcmFyZOmocT8ERG91Z+25cT8HQ2FtZXJvbvHKcT8FU2FkaWUl23E/B1Jvc2FsaWVZ63E/BVJvYnlujftxPwVLZW5uecELcj8DSXJh9RtyPwZIdWJlcnQpLHI/BkJyb29rZV08cj8HQmV0aGFueZFMcj8KQmVybmFkZXR0ZcVccj8GQmVubmll+WxyPwdBbnRvbmlhLX1yPwhBbmdlbGljYWGNcj8JQWxleGFuZHJhlZ1yPwhBZHJpZW5uZcmtcj8FVHJhY2kuvXI/B1JhY2hhZWyTzHI/B05pY2hvbGX423I/Bk11cmllbF3rcj8ETWF0dML6cj8FTWFibGUnCnM/BEx5bGWMGXM/B0xhdmVybmXxKHM/BktlbmRyYVY4cz8HSmFzbWluZbtHcz8JRXJuZXN0aW5lIFdzPwdDaGVsc2VhhWZzPwdBbGZvbnNv6nVzPwNSZXhPhXM/B09ybGFuZG+0lHM/BU9sbGllGaRzPwROZWFsfrNzPwhNYXJjZWxsYePCcz8FTG9yZW5I0nM/B0tyeXN0YWyt4XM/B0VybmVzdG8S8XM/BUVsZW5hdwB0PwdDYXJsdG9u3A90PwVCbGFrZUEfdD8IQW5nZWxpbmGmLnQ/BldpbGJ1cjw9dD8GVGF5bG9y0kt0PwZTaGVsYnloWnQ/BFJ1ZHn+aHQ/CFJvZGVyaWNrlHd0PwhQYXVsZXR0ZSqGdD8FUGFibG/AlHQ/BE9tYXJWo3Q/BE5vZWzssXQ/Bk5hZGluZYLAdD8HTG9yZW56bxjPdD8ETG9yYa7ddD8FTGVpZ2hE7HQ/BEthcmna+nQ/BkhvcmFjZXAJdT8FR3JhbnQGGHU/B0VzdGVsbGWcJnU/BkRpYW5uYTI1dT8GV2lsbGlzyEN1PwlSb3NlbWFyaWVeUnU/BlJpY2tlefRgdT8ETW9uYYpvdT8GS2VsbGV5IH51PwZEb3JlZW62jHU/B0Rlc2lyZWVMm3U/B0FicmFoYW3iqXU/B1J1ZG9scGh4uHU/B1ByZXN0b24Ox3U/B01hbGNvbG2k1XU/BktlbHZpbjrkdT8JSm9obmF0aGFu0PJ1PwVKYW5pc2YBdj8ESG9wZfwPdj8GR2luZ2Vykh52PwVGcmVkYSgtdj8FRGFtb26+O3Y/CENocmlzdGllVEp2PwVDZXNhcupYdj8FQmV0c3mAZ3Y/BkFuZHJlcxZ2dj8CV23cg3Y/BlRvbW1pZaKRdj8EVGVyaWifdj8GUm9iYmllLq12PwhNZXJlZGl0aPS6dj8ITWVyY2VkZXO6yHY/BU1hcmNvgNZ2PwdMeW5ldHRlRuR2PwRFdWxhDPJ2PwhDcmlzdGluYdL/dj8GQXJjaGllmA13PwVBbHRvbl4bdz8GU29waGlhJCl3PwhSb2NoZWxsZeo2dz8IUmFuZG9scGiwRHc/BFBldGV2Unc/BU1lcmxlPGB3PwZNZWdoYW4Cbnc/CEpvbmF0aG9uyHt3PwhHcmV0Y2hlbo6Jdz8HR2VyYXJkb1SXdz8IR2VvZmZyZXkapXc/BUdhcnJ54LJ3PwZGZWxpcGWmwHc/BkVsb2lzZWzOdz8CRWQy3Hc/B0RvbWluaWP46Xc/BURldmluvvd3PwdDZWNlbGlhhAV4PwdDYXJyb2xsShN4PwZSYXF1ZWxBIHg/BUx1Y2FzOC14PwRKYW5hLzp4PwlIZW5yaWV0dGEmR3g/BEd3ZW4dVHg/CUd1aWxsZXJtbxRheD8HRWFybmVzdAtueD8HRGVsYmVydAJ7eD8FQ29saW75h3g/BkFseXNzYfCUeD8GVHJpY2lhF6F4PwVUYXNoYT6teD8HU3BlbmNlcmW5eD8HUm9kb2xmb4zFeD8FT2xpdmWz0Xg/BU15cm9u2t14PwVKZW5uYQHqeD8GRWRtdW5kKPZ4PwRDbGVvTwJ5PwVCZW5ueXYOeT8GU29waGllnRp5PwVTb25qYcQmeT8GU2lsdmlh6zJ5PwlTYWx2YXRvcmUSP3k/BVBhdHRpOUt5PwVNaW5keWBXeT8DTWF5h2N5PwVNYW5kea5veT8GTG93ZWxs1Xt5PwZMb3JlbmH8h3k/BExpbGEjlHk/BExhbmFKoHk/BktlbGxpZXGseT8ES2F0ZZi4eT8FSmV3ZWy/xHk/BUdyZWdn5tB5PwdHYXJyZXR0Dd15PwVFc3NpZTTpeT8GRWx2aXJhW/V5PwVEZWxpYYIBej8FRGFybGGpDXo/BkNlZHJpY9AZej8GV2lsc29u9yV6PwlTeWx2ZXN0ZXIeMno/B1NoZXJtYW5FPno/BVNoYXJpbEp6PwlSb29zZXZlbHSTVno/B01pcmFuZGG6Yno/BU1hcnR54W56PwVNYXJ0YQh7ej8FTHVjaWEvh3o/BkxvcmVuZVaTej8ETGVsYX2fej8ISm9zZWZpbmGkq3o/B0pvaGFubmHLt3o/CEplcm1haW5l8sN6PwdKZWFubmllGdB6PwZJc3JhZWxA3Ho/BUZhaXRoZ+h6PwRFbHNhjvR6PwVEaXhpZbUAez8HQ2FtaWxsZdwMez8IV2luaWZyZWQ0GHs/B1dpbGJlcnSMI3s/BFRhbWnkLns/B1RhYml0aGE8Ons/BlNoYXduYZRFez8EUmVuYexQez8DT3JhRFx7PwZOZXR0aWWcZ3s/BU1lbGJh9HJ7PwZNYXJpbmFMfns/BkxlbGFuZKSJez8HS3Jpc3RpZfyUez8HRm9ycmVzdFSgez8FRWxpc2Gsq3s/BUVib255BLd7PwZBbGlzaGFcwns/BUFpbWVltM17PwZUYW1taWU82Hs/BVNpbW9uxOJ7PwdTaGVycmllTO17PwVTYW1tedT3ez8FUm9uZGFcAnw/B1BhdHJpY2XkDHw/BE93ZW5sF3w/BU15cm5h9CF8PwVNYXJsYXwsfD8HTGF0YXNoYQQ3fD8GSXJ2aW5njEF8PwZEYWxsYXMUTHw/BUNsYXJrnFZ8PwZCcnlhbnQkYXw/BkJvbml0YaxrfD8GQXVicmV5NHZ8PwVBZGRpZbyAfD8HV29vZHJvd0SLfD8GU3RhY2llzJV8PwVSdWZ1c1SgfD8HUm9zYXJpb9yqfD8HUmViZWthaGS1fD8GTWFyY29z7L98PwRNYWNrdMp8PwRMdXBl/NR8PwdMdWNpbmRhhN98PwNMb3UM6nw/BExldmmU9Hw/CExhdXJlbmNlHP98PwpLcmlzdG9waGVypAl9PwZKZXdlbGwsFH0/BEpha2W0Hn0/B0d1c3Rhdm88KX0/CEZyYW5jaW5lxDN9PwVFbGxpc0w+fT8ERHJld9RIfT8GRG9ydGh5XFN9PwdEZWxvcmlz5F19PwVDaGVyaWxofT8HQ2VsZXN0ZfRyfT8EQ2FyYXx9fT8HQWRyaWFuYQSIfT8FQWRlbGWMkn0/B0FiaWdhaWwUnX0/BlRyaXNoYZynfT8FVHJpbmEksn0/BlRyYWNpZay8fT8GU2FsbGllNMd9PwRSZWJhvNF9PwdPcnZpbGxlRNx9PwVOaWtraczmfT8HTmljb2xhc1TxfT8HTWFyaXNzYdz7fT8HTG91cmRlc2QGfj8GTG90dGll7BB+PwZMaW9uZWx0G34/Bkxlbm9yYfwlfj8GTGF1cmVshDB+PwVLZXJyaQw7fj8GS2Vsc2V5lEV+PwVLYXJpbhxQfj8FSm9zaWWkWn4/B0phbmVsbGUsZX4/BklzbWFlbLRvfj8GSGVsZW5lPHp+PwhHaWxiZXJ0b8SEfj8ER2FsZUyPfj8JRnJhbmNpc2Nh1Jl+PwRGZXJuXKR+PwRFdHRh5K5+PwdFc3RlbGxhbLl+PwRFbHZh9MN+PwVFZmZpZXzOfj8JRG9taW5pcXVlBNl+PwdDb3Jpbm5ljON+PwVDbGludBTufj8IQnJpdHRuZXmc+H4/BkF1cm9yYSQDfz8HV2lsZnJlZN0Mfz8FVG9tYXOWFn8/BFRvYnlPIH8/B1NoZWxkb24IKn8/BlNhbnRvc8Ezfz8FTWF1ZGV6PX8/Bkxlc2xleTNHfz8ESm9zaOxQfz8HSmVuaWZlcqVafz8DSXZhXmR/PwZJbmdyaWQXbn8/A0luYdB3fz8HSWduYWNpb4mBfz8ESHVnb0KLfz8GR29sZGll+5R/PwdFdWdlbmlhtJ5/PwVFcnZpbm2ofz8FRXJpY2smsn8/CUVsaXNhYmV0aN+7fz8FRGV3ZXmYxX8/B0NocmlzdGFRz38/BkNhc3NpZQrZfz8EQ2FyecPifz8FQ2FsZWJ87H8/B0NhaXRsaW419n8/BkJldHRpZe7/fz8=");
        #endregion
        
        private List<KeyValuePair<string, float>> m_names = null;

        public override string Generate(ulong seed)
        {
            if (m_names == null || m_names.Count == 0) return "{no names}";
            var nf = seed / (float)ulong.MaxValue;
            // binary search name list.
            var left = 0;
            var right = m_names.Count - 1;
            while (left < right)
            {
                var m = (left + right) / 2;
                if (m_names[m].Value > nf)
                    right = m - 1;
                else
                    left = m + 1;
            }
            return m_names[left].Key;
        }

        public override void LoadConfiguration(Ob_ModSessionComponent configOriginal)
        {
            var config = configOriginal as Ob_StatisticalNameGenerator;
            if (config == null)
            {
                Log(MyLogSeverity.Critical, "Configuration type {0} doesn't match component type {1}", configOriginal.GetType(),
                    GetType());
                return;
            }
            if (config.StatisticsDatabase.Equals("res:english", StringComparison.OrdinalIgnoreCase))
                m_names = EnglishNames;
            else
                m_names = UnpackDictionary(config.StatisticsDatabase);
        }

        public override Ob_ModSessionComponent SaveConfiguration()
        {
            string database = null;
            if (m_names == EnglishNames)
                database = "res:english";
            else
                database = PackDictionary(m_names);
            return new Ob_StatisticalNameGenerator() { StatisticsDatabase = database };
        }
    }

    public class Ob_StatisticalNameGenerator : Ob_NameGeneratorBase
    {
        public string StatisticsDatabase = "res:english";
    }
}
