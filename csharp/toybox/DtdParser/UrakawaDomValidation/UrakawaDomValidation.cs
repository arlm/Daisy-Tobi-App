﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using urakawa;
using urakawa.core;
using urakawa.daisy.import;
using urakawa.xuk;
using DtdParser;

namespace UrakawaDomValidation
{
    public class TestValidation
    {
        public static void Main(string[] args)
        {
            string dtd = @"..\..\..\dtbook-2005-3.dtd";
            string simplebook = @"..\..\..\simplebook.xuk";
            string paintersbook = @"..\..\..\greatpainters.xuk";
            string dtdcache = @"..\..\..\dtd.cache";

            UrakawaDomValidation validator = new UrakawaDomValidation();
            bool readFromCache = false;

            if (readFromCache)
            {
                validator.UseCachedDtd(new StreamReader(Path.GetFullPath(dtdcache)));
            }
            else
            {
                validator.UseDtd(dtd);
                validator.SaveCachedDtd(new StreamWriter(Path.GetFullPath(dtdcache)));
            }

            Project project = LoadXuk(paintersbook);
            TreeNode root = project.Presentations.Get(0).RootNode;
            bool res = validator.Validate(root);

            if (res) Console.WriteLine("VALID!!");
            else Console.WriteLine("INVALID!!");

            Console.WriteLine("Press any key to continue");
            Console.ReadKey(); 
        }

        public static Project LoadXuk(string file)
        {
            string fullpath = Path.GetFullPath(file);
            Project project = new Project();
            var uri = new Uri(fullpath);
            var action = new OpenXukAction(project, uri);
            action.Execute();
            return project;
        }

    }
    public class UrakawaDomValidation
    {
        private DTD m_Dtd;
        private Hashtable m_DtdElementRegExp;

        
        public void UseDtd(string dtd)
        {
            StreamReader reader = new StreamReader(dtd);
            UseDtd(reader);
        }
        public void UseDtd(StreamReader dtd)
        {
            DTDParser parser = new DTDParser(dtd);
            m_Dtd = parser.Parse(true);
            ParseDtdIntoHashmap();
        }
        public void UseCachedDtd(StreamReader dtd)
        {
            ReadCachedDtdIntoHashmap(dtd);
        }
        public void SaveCachedDtd(StreamWriter dtd)
        {
            WriteDtdToCache(dtd);
        }
        private void ParseDtdIntoHashmap()
        {
            m_DtdElementRegExp = new Hashtable();

            foreach(DictionaryEntry entry in m_Dtd.Elements)
            {
                DTDElement dtdElement = (DTDElement) entry.Value;
                string regExpStr = GenerateRegExpForAllowedChildren(dtdElement.Content);
                Regex regExp = new Regex(regExpStr);
                m_DtdElementRegExp.Add(dtdElement.Name, regExp);
            }
        }

        //The cached file format is:
        //ElementName
        //Regex of allowed children
        //...
        private void ReadCachedDtdIntoHashmap(StreamReader cachedDtd)
        {
            m_DtdElementRegExp = new Hashtable();
            string name = cachedDtd.ReadLine();
            string regExpStr = cachedDtd.ReadLine();
            
            while (name != null && regExpStr != null)
            {
                Regex regEx = new Regex(regExpStr);
                m_DtdElementRegExp[name] = regEx;
                name = cachedDtd.ReadLine();
                regExpStr = cachedDtd.ReadLine();
            }

            cachedDtd.Close();
            
        }
        private void WriteDtdToCache(StreamWriter cachedDtd)
        {
            foreach (DictionaryEntry entry in m_DtdElementRegExp)
            {
                string name = (string)entry.Key;
                string regExpStr = ((Regex)entry.Value).ToString();
                cachedDtd.WriteLine(name);
                cachedDtd.WriteLine(regExpStr);
            }
            cachedDtd.Close();
        }
        private string GenerateRegExpForAllowedChildren(DTDItem dtdItem)
        {
            string regExpStr = "";

            if (dtdItem is DTDAny)
            {
                regExpStr += "Any";
            }
            else if (dtdItem is DTDEmpty)
            {
                regExpStr += "";
            }
            else if (dtdItem is DTDName)
            {
                regExpStr += "(?:" + ((DTDName)dtdItem).Value + "#)";
            }
            else if (dtdItem is DTDChoice)
            {
                List<DTDItem> items = ((DTDChoice)dtdItem).Items;
                if (items.Count > 1) regExpStr += "(?:";

                bool isFirst = true;
                foreach (DTDItem item in items)
                {
                    if (!isFirst) regExpStr += "|";
                    regExpStr += GenerateRegExpForAllowedChildren(item);
                    isFirst = false;
                }
                if (items.Count > 1) regExpStr += ")";
            }
            else if (dtdItem is DTDSequence)
            {
                List<DTDItem> items = ((DTDSequence)dtdItem).Items;
                if (items.Count > 1) regExpStr += "(?:";

                bool isFirst = true;
                foreach (DTDItem item in items)
                {
                    if (!isFirst) regExpStr += "";
                    regExpStr += GenerateRegExpForAllowedChildren(item);
                    isFirst = false;
                }
                if (items.Count > 1) regExpStr += ")";
            }
            else if (dtdItem is DTDMixed)
            {
                List<DTDItem> items = ((DTDMixed)dtdItem).Items;
                if (items.Count > 1) regExpStr += "(?:";

                bool isFirst = true;
                foreach (DTDItem item in items)
                {
                    if (!isFirst) regExpStr += "";
                    regExpStr += GenerateRegExpForAllowedChildren(item);
                    isFirst = false;
                }
                if (items.Count > 1) regExpStr += ")";
            }
            else if (dtdItem is DTDPCData)
            {
                regExpStr += "#PCDATA";
            }
            else
            {
                regExpStr += "**UNKNOWN**";
            }
            if (dtdItem.Cardinal == DTDCardinal.ZEROONE)
            {
                regExpStr += "?";
            }
            else if (dtdItem.Cardinal == DTDCardinal.ZEROMANY)
            {
                regExpStr += "*";
            }
            else if (dtdItem.Cardinal == DTDCardinal.ONEMANY)
            {
                regExpStr += "+";
            }
            return regExpStr;
        }
        
        //the recursive function
        public bool Validate(TreeNode node)
        {
            bool result = ValidateNodeContent(node);
            foreach (TreeNode child in node.Children.ContentsAs_ListAsReadOnly)
            {
                result = result & Validate(child);
            }
            return result;
        }
        //check a single node
        private bool ValidateNodeContent(TreeNode node)
        {
            string childrenNames = GetChildrenNames(node);
            Regex regExp = (Regex) m_DtdElementRegExp[node.GetXmlElementQName().LocalName];
            return regExp.IsMatch(childrenNames);
        }

        private string GetChildrenNames(TreeNode node)
        {
            string names = "";
            foreach (TreeNode child in node.Children.ContentsAs_ListAsReadOnly)
            {
                names += child.GetXmlElementQName().LocalName + "#";
            }
            return names;
        }
    }
}
