using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace ExtensionManagerLibrary
{
    enum attribute
    {
        None, Type, Method, Property, Event
    }

    class Group : IComparable
    {
        public Member member;
        public List<Member> members;

        public Group()
        {
            members = new List<Member>();
        }

        int IComparable.CompareTo(object obj)
        {
            return member.header.text.CompareTo(((Group)obj).member.header.text);
        }
    }

    class Member : IComparable
    {
        public Header header;
        public List<Node> nodes;

        public Member()
        {
            nodes = new List<Node>();
        }

        int IComparable.CompareTo(object obj)
        {
            if (null == header.text || null == obj) return 0;
            return header.text.CompareTo(((Member)obj).header.text);
        }
    }

    class Node
    {
        public string type;
        public string name;
        public string value;

        public Node()
        {
            type = "";
            name = null;
            value = "";
        }
    }

    class Header
    {
        public attribute attrib;
        public string text;
    }

    class Parser
    {
        List<string> exclude = new List<string>();
        string extName;
        XmlTextReader reader;
        string assembly;
        List<Group> groups;
        Group group;
        Member member;
        Node node;
        //Translate translate = new Translate();

        public Parser(string xmlFile, string extName)
        {
            GetExludes();

            this.extName = extName;
            try
            {
                reader = new XmlTextReader(xmlFile);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK);
                reader = null;
            }
            groups = Parse();
        }

        public string writeHTML(bool bSeparateFiles)
        {
            string path = Path.GetTempPath() + "SBExtension_API";
            string htmlFile = path + "\\" + extName + ".html";

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);

                    string css = Properties.Resources.styleAPI;
                    StreamWriter sw = getStreamWriter(path + "\\styleAPI.css", Encoding.ASCII);
                    if (null == sw) return null;
                    sw.WriteLine(css);
                    sw.Close();

                    if (!Directory.Exists(path + "\\images"))
                    {
                        Directory.CreateDirectory(path + "\\images");
                    }
                    System.Drawing.Bitmap dImg;
                    System.Drawing.Icon dIcon;
                    FileStream fs;

                    dImg = Properties.Resources.background;
                    fs = new FileStream(path + "\\images\\background.png", FileMode.Create);
                    dImg.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                    fs.Close();

                    dImg = Properties.Resources.IntellisenseEvent;
                    fs = new FileStream(path + "\\images\\IntellisenseEvent.png", FileMode.Create);
                    dImg.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                    fs.Close();

                    dImg = Properties.Resources.IntellisenseMethod;
                    fs = new FileStream(path + "\\images\\IntellisenseMethod.png", FileMode.Create);
                    dImg.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                    fs.Close();

                    dImg = Properties.Resources.IntellisenseObject;
                    fs = new FileStream(path + "\\images\\IntellisenseObject.png", FileMode.Create);
                    dImg.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                    fs.Close();

                    dImg = Properties.Resources.IntellisenseProperty;
                    fs = new FileStream(path + "\\images\\IntellisenseProperty.png", FileMode.Create);
                    dImg.Save(fs, System.Drawing.Imaging.ImageFormat.Png);
                    fs.Close();

                    dIcon = Properties.Resources.SBIcon;
                    fs = new FileStream(path + "\\favicon.ico", FileMode.Create);
                    dIcon.Save(fs);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Small Basic Extension Manager Error", MessageBoxButton.OK);
            }

            int nFiles = bSeparateFiles ? groups.Count : 1;
            for (int iFile = bSeparateFiles ? -1 : 0; iFile < nFiles; iFile++)
            {
                string file = bSeparateFiles && iFile >= 0 ? extName + "_" + groups[iFile].member.header.text : extName;
                StreamWriter sw = getStreamWriter(path + "\\" + file + ".html", Encoding.UTF8);
                if (null == sw) return null;
                writeHeader(sw, bSeparateFiles, extName);
                string args;
                for (int jFile = 0; jFile < groups.Count; jFile++)
                {
                    if (bSeparateFiles && (iFile != jFile)) continue;
                    Group group = groups[jFile];
                    Member member1 = group.member;
                    writeLine(sw, member1.header.text, member1.header.text, "group");
                    foreach (Node node1 in member1.nodes)
                    {
                        if (!node1.type.ToLower().Contains("summary")) writeLine(sw, node1.type, "", "info");
                        writeLine(sw, node1.value, "", "text");
                    }
                    group.members.Sort();
                    sw.WriteLine("<br />");
                    sw.WriteLine("<table class=\"table\"  cellpadding=\"4\">");
                    int i = 0;
                    foreach (Member member2 in group.members)
                    {
                        string text = member2.header.text;
                        if (i % 3 == 0) sw.WriteLine("<tr>");
                        sw.WriteLine("<td>");
                        sw.WriteLine("<a href=\"#" + member1.header.text + text + "\">" + text + "</a>");
                        if (member2.header.attrib == attribute.Method) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseMethod.png\" />");
                        else if (member2.header.attrib == attribute.Property) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseProperty.png\" />");
                        else if (member2.header.attrib == attribute.Event) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseEvent.png\" />");
                        sw.WriteLine("</td>");
                        i++;
                        if (i % 3 == 0) sw.WriteLine("</tr>");
                    }
                    if (i % 3 != 0) sw.WriteLine("</tr>");
                    sw.WriteLine("</table>");
                    foreach (Member member2 in group.members)
                    {
                        args = "";
                        if (member2.header.attrib == attribute.Method)
                        {
                            args = "(";
                            foreach (Node node2 in member2.nodes)
                            {
                                if (null != node2.name)
                                {
                                    args += node2.name + ",";
                                }
                            }
                            if (args.EndsWith(",")) args = args.Substring(0, args.Length - 1);
                            args += ")";
                        }
                        if (member2.header.text == "Dispose") continue;
                        if (member2.header.text == "InitializeComponent") continue;
                        writeLine(sw, member2.header.text, member1.header.text + member2.header.text, "method");
                        writeLine(sw, args, "", "methodargs", member2.header.attrib);
                        foreach (Node node3 in member2.nodes)
                        {
                            if (null == node3.name)
                            {
                                if (!node3.type.ToLower().Contains("summary")) writeLine(sw, node3.type, "", "info");
                            }
                            else
                            {
                                writeLine(sw, node3.name, "", "info");
                            }
                            writeLine(sw, node3.value, "", "text");
                        }
                    }
                }
                writeFooter(sw);
                sw.Close();
            }

            return htmlFile;
        }

        private void GetExludes()
        {
            exclude.Clear();
            //LitDev
            exclude.Add("LDWeather");
            exclude.Add("Capture");
            exclude.Add("Defines");
            exclude.Add("FIP");
            //LitDev3D
            exclude.Add("Resources");
            //SmallBasicLibrary
            exclude.Add("DiscoveryCompletedEventArgs");
            exclude.Add("DiscoveryCompletedEventHandler");
            exclude.Add("Keywords");
            exclude.Add("NativeHelper");
            exclude.Add("QueryCompletedEventArgs");
            exclude.Add("QueryCompletedEventHandler");
            exclude.Add("RegistrationCompletedEventArgs");
            exclude.Add("RegistrationCompletedEventHandler");
            exclude.Add("RestHelper");
            exclude.Add("SmallBasicApplication");
            exclude.Add("SmallBasicCallback");
            exclude.Add("StatusCompletedEventArgs");
            exclude.Add("StatusCompletedEventHandler");
            exclude.Add("Primitive");
            exclude.Add("Platform");
            exclude.Add("OfficeResearch");
            exclude.Add("#ctor");
            exclude.Add("Dispose");
            exclude.Add("HidD_GetHidGuid");
            exclude.Add("LPF1");
        }

        private List<Group> Parse()
        {
            if (null == reader) return null;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            if (reader.Name == "assembly")
                            {
                                groups = new List<Group>();
                                assembly = "";
                            }
                            else if (reader.Name == "member" && reader.HasAttributes)
                            {
                                if (null == groups) continue;
                                for (int i = 0; i < reader.AttributeCount; i++)
                                {
                                    reader.MoveToAttribute(i);
                                    if (reader.Name == "name")
                                    {
                                        member = new Member();
                                        member.header = getHeader(reader.Value);

                                        if (member.header.attrib == attribute.Type)
                                        {
                                            group = new Group();
                                            group.member = member;
                                            groups.Add(group);
                                        }
                                        else if (member.header.attrib == attribute.None)
                                        {
                                            group = null;
                                        }
                                        else if (null != group)
                                        {
                                            group.members.Add(member);
                                        }
                                        else if (member.header.attrib != attribute.None)
                                        {
                                            //if not type and no group then create a dummy group of attribute type
                                            //TODO better

                                            group = new Group();
                                            member.header.attrib = attribute.Type;
                                            group.member = member;
                                            groups.Add(group);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (null == member) continue;
                                node = new Node();
                                node.type = reader.Name;
                                for (int i = 0; i < reader.AttributeCount; i++)
                                {
                                    reader.MoveToAttribute(i);
                                    if (reader.Name == "name")
                                    {
                                        node.name = reader.Value;
                                    }
                                }
                                member.nodes.Add(node);
                            }
                        }
                        break;
                    case XmlNodeType.Text:
                        if (assembly == "") assembly = reader.Value;
                        if (null == node) continue;
                        node.value = reader.Value;
                        node.value = node.value.Replace("\r\n            ", "\r\n");
                        node.value = node.value.Trim(new char[] { '\r', '\n' });
                        //TODO - The following is too slow
                        //node.value = translate.TranslateMethod(node.value, "en-gb", "de-de");
                        break;
                    case XmlNodeType.EndElement:
                        break;
                }
            }

            if (null == groups) return groups;
            groups.Sort();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                Group group1 = groups[i];
                //Default exclusions
                foreach (string ex in exclude)
                {
                    if (group1.member.header.text.ToLower() == ex.ToLower()) groups.Remove(group1);
                }
            }
            return groups;
        }

        private StreamWriter getStreamWriter(string fileName, Encoding encoding)
        {
            try
            {
                return new StreamWriter(fileName, false, encoding);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK);
            }
            return null;
        }

        private void writeHeader(StreamWriter sw, bool bSeparateFiles, string caseName)
        {
            //sw.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">"); 
            sw.WriteLine("<!DOCTYPE html>");
            sw.WriteLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            sw.WriteLine("<head>");
            sw.WriteLine(" <meta http-equiv=\"Content-type\" content=\"text/html;charset=UTF-8\">");
            sw.WriteLine(" <title>" + assembly + " API</title>");
            sw.WriteLine(" <meta name=\"description\" content=\"SmallBasic " + assembly + " extension API\" />");
            sw.WriteLine(" <meta name=\"keywords\" content=\"SmallBasic,litdev," + assembly + "extension\" />");
            sw.WriteLine(" <link rel=\"stylesheet\" type=\"text/css\" href=\"styleAPI.css\" />");
            sw.WriteLine(" <link rel=\"shortcut icon\" href=\"favicon.ico\" />");
            sw.WriteLine("</head>");
            sw.WriteLine("<body>");
            sw.WriteLine("<div id=\"wrapper\">");
            sw.WriteLine("<div id=\"content\">");
            sw.WriteLine();
            writeLine(sw, assembly, "", "assembly");
            sw.WriteLine("<table class=\"table\" cellpadding=\"4\">");
            int i = 0;
            foreach (Group group in groups)
            {
                string text = group.member.header.text;
                if (i % 5 == 0) sw.WriteLine("<tr>");
                sw.WriteLine("<td>");
                string file = bSeparateFiles ? caseName + "_" + text + ".html" : "#" + text;
                sw.WriteLine("<a href=\"" + file + "\">" + text + "</a>");
                sw.WriteLine("</td>");
                i++;
                if (i % 5 == 0) sw.WriteLine("</tr>");
            }
            if (i % 5 != 0) sw.WriteLine("</tr>");
            sw.WriteLine("</table>");
        }

        private void writeFooter(StreamWriter sw)
        {
            sw.WriteLine("<br />");
            sw.WriteLine("<div id=\"footer\">");
            sw.WriteLine("<hr style=\"height: 2px; width: 100%;\" />");
            //sw.WriteLine("<a style=\"position: relative; float: left;\" href=\"http://free-website-translation.com/\" id=\"ftwtranslation_button\" hreflang=\"en\" title=\"\" style=\"border:0;\"><img src=\"http://free-website-translation.com/img/fwt_button_en.gif\" id=\"ftwtranslation_image\" alt=\"Website Translation Widget\" style=\"border:0;\"/></a> <script type=\"text/javascript\" src=\"http://free-website-translation.com/scripts/fwt.js\" /></script>");
            sw.WriteLine("</div>");
            sw.WriteLine("</div>");
            sw.WriteLine("</div>");
            sw.WriteLine("</body>");
            sw.WriteLine("</html>");
        }

        private void writeLine(StreamWriter sw, string text, string link, string style, attribute attrib = attribute.None)
        {
            if (null == text) return;
            if (style == "text" && text == "") return;
            text = text.Trim();
            text = text.Replace("\r\n", "<br />");

            Regex urlRx = new Regex("http://([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*_\\-\\=\\+\\\\\\/\\?\\.\\:\\;\\'\\,]*)?", RegexOptions.IgnoreCase);
            MatchCollection matches = urlRx.Matches(text);
            foreach (Match match in matches)
            {
                text = text.Replace(match.Value, "<a href=\"" + match.Value + "\">" + match.Value + "</a>");
            }

            if (style == "assembly" && text != "SmallBasicLibrary") text += " Extension";
            if (style == "assembly") text += " API";
            if (style == "group" || style == "method") sw.WriteLine("<br />");
            if (link != "")
            {
                sw.Write("<a class=\"tag\" name=\"" + link + "\"></a>");
            }
            if (style == "assembly")
            {
                sw.Write("<p class=\"" + style + "\">");
                sw.Write(text);
                sw.Write("</p>");
            }
            else
            {
                sw.Write("<span class=\"" + style + "\">");
                sw.Write(text);
                sw.Write("</span>");
            }
            if (attrib == attribute.Method) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseMethod.png\" />");
            else if (attrib == attribute.Property) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseProperty.png\" />");
            else if (attrib == attribute.Event) sw.WriteLine(" <img alt=\"\" height=\"20px\" src=\"images/IntellisenseEvent.png\" />");
            else if (style == "group") sw.WriteLine(" <img alt=\"\" height=\"24px\" src=\"images/IntellisenseObject.png\" />");
            if (style == "info")
            {
                sw.Write(" ");
            }
            else if (style == "assembly")
            {
                sw.WriteLine();
            }
            else if (style != "method")
            {
                sw.WriteLine("<br />");
            }
        }

        private Header getHeader(string txt)
        {
            Header header = new Header();
            if (txt.StartsWith("T:")) header.attrib = attribute.Type;
            else if (txt.StartsWith("M:")) header.attrib = attribute.Method;
            else if (txt.StartsWith("P:")) header.attrib = attribute.Property;
            else if (txt.StartsWith("E:")) header.attrib = attribute.Event;
            else header.attrib = attribute.None;

            //TODO better
            //if (!txt.Replace(' ','_').ToLower().Contains(extName.Replace(' ', '_').ToLower()) && extName != "SmallBasicLibrary") header.attrib = attribute.None;

            if (header.attrib != attribute.None)
            {
                if (txt.IndexOf("(") > 0) txt = txt.Substring(0, txt.IndexOf("("));
                header.text = txt.Split('.').Last();
            }
            return header;
        }
    }
}
