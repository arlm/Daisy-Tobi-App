﻿using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using urakawa;
using urakawa.core;
using urakawa.xuk;

namespace UrakawaDomValidation
{
    public class TestValidation
    {
        public static void Main(string[] args)
        {
            string dtd = @"..\..\..\dtbook-2005-3.dtd";

            //valid files
            string simplebook = @"..\..\..\simplebook.xuk";
            string paintersbook = @"..\..\..\greatpainters.xuk";

            //invalid files
            string simplebook_invalid_doublefrontmatter = @"..\..\..\simplebook-invalid-doublefrontmatter.xuk";
            string simplebook_invalid_unrecognized_element = @"..\..\..\simplebook-invalid-element.xuk";
            string simplebook_invalid_badnesting = @"..\..\..\simplebook-invalid-badnesting.xuk";

            //the dtd cache location (hardcoded in this example.  
            //eventually, we'll need a table of dtd to cached version)
            string dtdcache = @"..\..\..\dtd.cache";

            bool writeToLog = false;
            bool reportTraces = false;

            FileStream ostrm = null;
            StreamWriter writer = null;
            TextWriter oldOut = null;

            if (writeToLog)
            {
                oldOut = Console.Out;
                ostrm = new FileStream(@"..\..\..\log.txt", FileMode.OpenOrCreate, FileAccess.Write);
                writer = new StreamWriter(ostrm);
                Console.SetOut(writer);
            }

            UrakawaDomValidation validator = new UrakawaDomValidation();
            bool readFromCache = false;

            if (readFromCache)
            {
                validator.UseCachedDtd(new StreamReader(Path.GetFullPath(dtdcache)));
            }
            else
            {
                validator.UseDtd(new StreamReader(Path.GetFullPath(dtd)));
                validator.SaveCachedDtd(new StreamWriter(Path.GetFullPath(dtdcache)));
            }

            Project project = LoadXuk(simplebook_invalid_doublefrontmatter);
            TreeNode root = project.Presentations.Get(0).RootNode;
            bool res = validator.Validate(root);

            Console.WriteLine(res ? "VALID!!" : "INVALID!!");

            foreach (UrakawaDomValidationReportItem item in validator.ValidationReportItems)
            {
                if (item.ItemType == UrakawaDomValidationReportItem.ReportItemType.Error ||
                    (item.ItemType == UrakawaDomValidationReportItem.ReportItemType.Trace &&
                    reportTraces == true))
                {
                    string type = item.ItemType == UrakawaDomValidationReportItem.ReportItemType.Trace
                                      ? "TRACE"
                                      : "ERROR";
                    Console.WriteLine(string.Format("* {0}", type));
                    Console.WriteLine(string.Format("Node name: {0}", item.Node.GetXmlElementQName().LocalName));
                    Console.WriteLine(string.Format("Reg ex: {0}", validator.GetRegex(item.Node)));
                    Console.WriteLine(item.Message);
                    Console.WriteLine("\n");
                }
            }
            if (writeToLog)
            {
                Console.SetOut(oldOut);
                writer.Close();
                ostrm.Close();
                Console.WriteLine(res ? "VALID!!" : "INVALID!!");
            }

            string answer = "y";
            while (answer == "y")
            {
                RunCmdLineRegexTest(validator);
                Console.WriteLine("Test another element? (y/n)");
                answer = Console.ReadLine();
            }                
            
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
        public static bool TestRegex(UrakawaDomValidation validator, string element, string children)
        {
            string regexStr = validator.GetRegex(element);
            
            if (string.IsNullOrEmpty(regexStr))
            {
                //POSSIBLE RESULT #1
                Console.WriteLine("Element definition not found!");
                return false;
            }
            
            Regex regex = new Regex(regexStr);
            Match match = regex.Match(children);

            if (match.Success == true && match.ToString() == children)
            {
                //POSSIBLE RESULT #2
                Console.WriteLine("SUCCESS!");
                return true;
            }

            if (match.ToString() != children)
            {
                //test subsets of children -- 
                //for a,b,c test a,b then just a
                ArrayList childrenArr = new ArrayList(children.Split('#'));
                if (string.IsNullOrEmpty((string)childrenArr[childrenArr.Count -1]))
                    childrenArr.RemoveAt(childrenArr.Count - 1);
                if (childrenArr.Count - 2 >= 0)
                {
                    int i = 0;
                    string subchildren = "";
                    for (i = 0; i<childrenArr.Count-1; i++)
                    {
                        subchildren += childrenArr[i] + "#";
                    }
                    Console.WriteLine(string.Format("Testing subset {0}", subchildren));
                    TestRegex(validator, element, subchildren);
                }
                else
                {
                    //POSSIBLE RESULT #3
                    Console.WriteLine("Cannot test any more subsets.  Invalid.");
                    return false;
                }

                //POSSIBLE RESULT #4
                Console.WriteLine(string.Format("{0} is an invalid sequence for {1}", children, element));
                return false;
            }
            Console.WriteLine("Unspecified error.");
            return false;

        }

        public static void RunCmdLineRegexTest(UrakawaDomValidation validator)
        {
            Console.WriteLine("Enter an element name:");
            string elm = Console.ReadLine();

            Console.WriteLine("Enter the children for this element, separated by a comma:");
            string children = Console.ReadLine();
            children = children.Replace(", ", "#");
            children = children.Replace(",", "#");
            if (!children.EndsWith("#")) children += "#";

            TestRegex(validator, elm, children);
            
        }
    }
}
