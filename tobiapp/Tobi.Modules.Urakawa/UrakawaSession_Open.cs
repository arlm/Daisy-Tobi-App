﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using AudioLib;
using PipelineWSClient;
using Saxon.Api;
using Microsoft.Practices.Composite.Logging;
using Microsoft.Win32;
using Tobi.Common;
using Tobi.Common.MVVM;
using Tobi.Common.MVVM.Command;
using Tobi.Common.UI;
using urakawa;
using urakawa.core;
using urakawa.daisy.export;
using urakawa.daisy.import;
using urakawa.data;
using urakawa.exception;
using urakawa.media;
using urakawa.property.xml;
using urakawa.xuk;
using DocumentBuilder = Saxon.Api.DocumentBuilder;

#if ENABLE_OOXML
using DocumentFormat.OpenXml.Packaging;
using OpenXmlPowerTools;
#endif

using System.Drawing;
using System.Drawing.Imaging;
using System.Xml.Linq;
using System.Linq;

#if ENABLE_SHARPZIP
using ICSharpCode.SharpZipLib.Zip;
#else
using Jaime.Olivares;
#endif

namespace Tobi.Plugin.Urakawa
{
    public partial class UrakawaSession
    {
        private bool? waitForPipelineAlive(int timeout)
        {
            bool cancelled = false;
            var actionCancelled = (Action)(() =>
                {
                    cancelled = true;
                });

            var actionCompleted = (Action)(() =>
                {
                });

            var pipeAlive = new PipelineAlive(timeout);
            bool error = m_ShellView.RunModalCancellableProgressTask(true,
                                                                     "Pipeline2 start",
                                                                     pipeAlive,
                                                                     actionCancelled,
                                                                     actionCompleted
                );

            if (pipeAlive.TimedOut)
            {
                return false;
            }

            if (cancelled)
            {
                return null;
            }

            return true;
        }

        public RichDelegateCommand OpenCommand { get; private set; }
        public RichDelegateCommand ImportCommand { get; private set; }
        public RichDelegateCommand OpenConvertCommand { get; private set; }

#if ENABLE_OOXML
        public RichDelegateCommand DocXWordMLOpenXMLTransformCommand { get; private set; }
#endif

        private void initCommands_Open()
        {
            //NewCommand = new RichDelegateCommand(
            //    UserInterfaceStrings.New,
            //    UserInterfaceStrings.New_,
            //    UserInterfaceStrings.New_KEYS,
            //    shellView.LoadTangoIcon("document-new"),
            //    ()=>
            //    {
            //        string currentAssemblyDirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //        openFile(currentAssemblyDirectoryName + @"\empty-dtbook-z3986-2005.xml");
            //    },
            //    ()=> true);
            //shellView.RegisterRichCommand(NewCommand);
            //
            OpenCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdOpen_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdOpen_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon(@"document-open"),
                () =>
                {
                    var dlg = new OpenFileDialog
                    {
                        FileName = @"",
                        DefaultExt = @".opf",

                        Filter =
                            @"OBI, XUK, XUKSPINE"
 + " (*.obi, *" + OpenXukAction.XUK_EXTENSION + ", *" + OpenXukAction.XUK_SPINE_EXTENSION
 + ")|*.obi;*" + OpenXukAction.XUK_EXTENSION + ";*" + OpenXukAction.XUK_SPINE_EXTENSION
,
                        CheckFileExists = false,
                        CheckPathExists = false,
                        AddExtension = true,
                        DereferenceLinks = true,
                        Title =
                            @"Tobi: " +
                            UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.CmdOpen_ShortDesc)
                    };

                    bool? result = false;

                    m_ShellView.DimBackgroundWhile(() => { result = dlg.ShowDialog(); });

                    if (result == false)
                    {
                        return;
                    }
                    try
                    {
                        OpenFile(dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.Handle(ex, false, m_ShellView);
                    }
                },
                () => !isAudioRecording,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Open));

            m_ShellView.RegisterRichCommand(OpenCommand);
            //
            ImportCommand = new RichDelegateCommand(
                            Tobi_Plugin_Urakawa_Lang.CmdImport_ShortDesc,
                            Tobi_Plugin_Urakawa_Lang.CmdImport_LongDesc,
                            null, // KeyGesture obtained from settings (see last parameters below)
                            m_ShellView.LoadTangoIcon(@"document-open"),
                            () =>
                            {
                                var dlg = new OpenFileDialog
                                {
                                    FileName = @"",
                                    DefaultExt = @".opf",

                                    Filter =
                                        @"DTBook, XHTML, OPF, EPUB"
#if DEBUG
 + ", MML"
#endif
 + " (*.xml, *.xhtml, *.html, *.opf, *.epub"
#if DEBUG
 + ", *.mml"
#endif
 + ")|*.xml;*.xhtml;*.html;*.opf;*.epub"
#if DEBUG
 + ";*.mml"
#endif
,
                                    CheckFileExists = false,
                                    CheckPathExists = false,
                                    AddExtension = true,
                                    DereferenceLinks = true,
                                    Title =
                                        @"Tobi: " +
                                        UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.CmdImport_ShortDesc)
                                };

                                bool? result = false;

                                m_ShellView.DimBackgroundWhile(() => { result = dlg.ShowDialog(); });

                                if (result == false)
                                {
                                    return;
                                }
                                try
                                {
                                    OpenFile(dlg.FileName);
                                }
                                catch (Exception ex)
                                {
                                    ExceptionHandler.Handle(ex, false, m_ShellView);
                                }
                            },
                            () => !isAudioRecording,
                            Settings_KeyGestures.Default,
                            PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Import));

            m_ShellView.RegisterRichCommand(ImportCommand);
            //
#if ENABLE_OOXML
            DocXWordMLOpenXMLTransformCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdImportDocx_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdImportDocx_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon(@"preferences-desktop-font"),
                () =>
                {
                    m_ShellView.RaiseEscapeEvent();

                    //messageBoxText("Word XML conversion", "This feature is only available in the .NET4 version of Tobi!", "Because of .NET3 limitations, this functionality is currently disabled in this version of Tobi. Please download and install Tobi .NET4 (this is recommended anyway).");

                    var dlg = new OpenFileDialog
                    {
                        FileName = @"",
                        DefaultExt = @".docx",
                        Multiselect = true,
                        Filter = @"DOCX (*.docx)|*.docx",
                        CheckFileExists = false,
                        CheckPathExists = false,
                        AddExtension = true,
                        DereferenceLinks = true,
                        Title =
                            @"Tobi: " +
                            UserInterfaceStrings.EscapeMnemonic(
                                Tobi_Plugin_Urakawa_Lang.CmdImportDocx_ShortDesc)
                    };

                    bool? result = false;

                    m_ShellView.DimBackgroundWhile(() => { result = dlg.ShowDialog(); });

                    if (result == false)
                    {
                        return;
                    }

                    if (dlg.FileNames == null || dlg.FileNames.Length < 1)
                    {
                        return;
                    }

                    string ext = Path.GetExtension(dlg.FileNames[0]);

                    if (string.IsNullOrEmpty(ext)
                        || (
                               !ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                           ))
                    {
                        return;
                    }

                    string parentFolderPath = Path.Combine(Path.GetDirectoryName(dlg.FileNames[0]), Path.GetFileName(dlg.FileNames[0]) + @"_HTML");
                    if (!Directory.Exists(parentFolderPath))
                    {
                        FileDataProvider.CreateDirectory(parentFolderPath);
                    }
                    string htmlPath = Path.Combine(parentFolderPath, Path.GetFileNameWithoutExtension(dlg.FileNames[0]) + @".html");

                    using (WordprocessingDocument wDoc = WordprocessingDocument.Open(dlg.FileNames[0], true))
                    {
                        int imageCounter = 0;
                        var pageTitle = (string)wDoc.CoreFilePropertiesPart.GetXDocument().Descendants(DC.title).FirstOrDefault();
                        if (pageTitle == null)
                            pageTitle = Path.GetFileNameWithoutExtension(dlg.FileNames[0]);

                        HtmlConverterSettings settings = new HtmlConverterSettings()
                        {
                            PageTitle = pageTitle,
                            FabricateCssClasses = true,
                            CssClassPrefix = "pt-",
                            RestrictToSupportedLanguages = false,
                            RestrictToSupportedNumberingFormats = false,
                            ListItemImplementations = new Dictionary<string, Func<string, int, string, string>>()
                            {
                                {"fr-FR", ListItemTextGetter_fr_FR.GetListItemText},
                                {"tr-TR", ListItemTextGetter_tr_TR.GetListItemText},
                                {"ru-RU", ListItemTextGetter_ru_RU.GetListItemText},
                                {"sv-SE", ListItemTextGetter_sv_SE.GetListItemText},
                            },
                            ImageHandler = imageInfo =>
                            {
                                ++imageCounter;
                                string extension = imageInfo.ContentType.Split('/')[1].ToLower();

                                ImageFormat imageFormat = null;
                                if (extension == "png")
                                {
                                    imageFormat = ImageFormat.Png;
                                }
                                else if (extension == "gif")
                                {
                                    imageFormat = ImageFormat.Gif;
                                }
                                else if (extension == "bmp")
                                {
                                    imageFormat = ImageFormat.Bmp;
                                }
                                else if (extension == "jpeg")
                                {
                                    imageFormat = ImageFormat.Jpeg;
                                }
                                else if (extension == "tiff")
                                {
                                    extension = "png";
                                    imageFormat = ImageFormat.Png;
                                }
                                else if (extension == "x-wmf")
                                {
                                    extension = "png";
                                    imageFormat = ImageFormat.Png;
                                }

                                if (imageFormat == null)
                                {
#if DEBUG
                                    Debugger.Break();
#endif
                                    return null;
                                }

                                string imageFileName = "image_" + imageCounter.ToString() + "." + extension;
                                string imageFilePath = Path.Combine(parentFolderPath, imageFileName);

                                try
                                {
                                    imageInfo.Bitmap.Save(imageFilePath, imageFormat);
                                }
                                catch (Exception ex)
                                {
#if DEBUG
                                    Debugger.Break();
#endif
                                    return null;
                                }

                                XElement img = new XElement(Xhtml.img,
                                    new XAttribute(NoNamespace.src, imageFileName),
                                    imageInfo.ImgStyleAttribute,
                                    imageInfo.AltText != null ?
                                        new XAttribute(NoNamespace.alt, imageInfo.AltText) : null);
                                return img;
                            }
                        };


                        XElement html = HtmlConverter.ConvertToHtml(wDoc, settings);

                        // Note: the xhtml returned by ConvertToHtmlTransform contains objects of type
                        // XEntity.  PtOpenXmlUtil.cs define the XEntity class.  See
                        // http://blogs.msdn.com/ericwhite/archive/2010/01/21/writing-entity-references-using-linq-to-xml.aspx
                        // for detailed explanation.
                        //
                        // If you further transform the XML tree returned by ConvertToHtmlTransform, you
                        // must do it correctly, or entities will not be serialized properly.

                        var htmlString = html.ToString(SaveOptions.None);
                        File.WriteAllText(htmlPath, htmlString, Encoding.UTF8);
                    }
                },
                () => !isAudioRecording,
                Settings_KeyGestures.Default,
                null //PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_ImportDocx)
                );

            m_ShellView.RegisterRichCommand(DocXWordMLOpenXMLTransformCommand);
#endif
            //
            OpenConvertCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdOpenConvert_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdOpenConvert_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon(@"applications-games"),
                () =>
                {
                    m_ShellView.RaiseEscapeEvent();

                    if (!String.IsNullOrEmpty(Settings.Default.Pipeline2Url))
                    {
                        Resources.baseUri = Settings.Default.Pipeline2Url + "/ws";
                    }

                    string pipeline_ExePath = obtainPipelineExe();
                    if (string.IsNullOrEmpty(pipeline_ExePath))
                    {
                        return;
                    }

                    string workingDir = Path.GetDirectoryName(pipeline_ExePath);
                    //Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    if (!Settings.Default.Pipeline2OldExe)
                    {
                        //workingDir = Path.GetDirectoryName(workingDir);
                        //workingDir = Path.Combine(workingDir, "bin");
                        //pipeline_ExePath = Path.Combine(workingDir, "pipeline2.bat");

                        XmlDocument xmlDoc = null;
                        try
                        {
                            xmlDoc = Resources.Alive();
                        }
                        catch (Exception ex1)
                        {
                            m_Logger.Log(ex1.Message, Category.Debug, Priority.Medium);


                            m_Logger.Log(
                                String.Format(@"Starting Pipeline2 server ({0})", pipeline_ExePath),
                                Category.Debug, Priority.Medium);

                            try
                            {
                                JavaXmx mem_ = Settings.Default.Pipeline2JavaMaxMem;
                                var mem = (ushort)mem_;
                                string xmx = mem == 1024 ? "1G" : (mem == 512 ? "512M" : "1G");
                                string javaOpts = "-Xmx" + xmx + " -XX:MaxPermSize=256M -Dcom.sun.management.jmxremote";
                                string envJavaOpts = Environment.GetEnvironmentVariable("JAVA_OPTS", EnvironmentVariableTarget.Process);

                                if (string.IsNullOrEmpty(envJavaOpts) || envJavaOpts != javaOpts)
                                {
                                    Environment.SetEnvironmentVariable("JAVA_OPTS", javaOpts, EnvironmentVariableTarget.Process);
                                }
                            }
                            catch (Exception ex)
                            {
                                m_Logger.Log(
                                    String.Format(@"JAVA_OPTS failed ({0})", ex.Message),
                                    Category.Debug, Priority.Medium);
                            }

                            Thread thread = new Thread(new ThreadStart(() =>
                            {
                                m_ShellView.ExecuteShellProcess(pipeline_ExePath);

                                //executeProcess(
                                //    workingDir,
                                //    "DAISY Pipeline Server",
                                //    pipeline_ExePath,
                                //    null,
                                //    null);
                            }));
                            thread.Name = "Pipeline2 BAT execute";
                            thread.Priority = ThreadPriority.Normal;
                            thread.IsBackground = true;
                            thread.Start();

                            Thread.Sleep(1000);

                            m_ShellView.Activate();

                            int timeout = (int)Settings.Default.Pipeline2Timeout;
                            bool? waitResult = waitForPipelineAlive(timeout);
                            if (waitResult == null)
                            {
                                return; // cancelled
                            }

                            if (waitResult == false) // timeout
                            {
                            tryAgain:
                                //if (true || askUser("Start Pipeline2 manually?", pipeline_ExePath))
                                {
                                    messageBoxText("Pipeline2 timeout", "Please try again later, or run [pipeline2.bat] manually.", pipeline_ExePath);

                                    m_ShellView.ExecuteShellProcess(workingDir);
                                }

                                waitResult = waitForPipelineAlive(timeout);
                                if (waitResult == null)
                                {
                                    return; // cancelled
                                }
                                if (waitResult == false) // timeout
                                {
                                    goto tryAgain;
                                }
                            }

                            try
                            {
                                xmlDoc = Resources.Alive();
                            }
                            catch (Exception ex2)
                            {
                                m_Logger.Log(ex2.Message, Category.Debug, Priority.Medium);

                                m_Logger.Log(
                                    String.Format(@"Pipeline2 server not started! ({0})", pipeline_ExePath),
                                    Category.Debug, Priority.Medium);

                                //messageBoxText("Pipeline2", "Please run pipeline2.bat (double click).",
                                //               ex2.Message + Environment.NewLine + ex2.StackTrace);

                                m_ShellView.ExecuteShellProcess(workingDir);

#if DEBUG
                                Debugger.Break();
#endif

                                return;
                            }
                        }

                        if (xmlDoc != null)
                        {
                            m_PipelineWasStarted = true;

                            m_Logger.Log(String.Format(@"Pipeline2 server alive. ({0})", Resources.baseUri),
                                         Category.Debug, Priority.Medium);

                            MainClass.PrettyPrint(xmlDoc);
                        }
                        else
                        {
                            return;
                        }
                    }


                    //messageBoxText("Pipeline2", "Please choose a source file...", "OPF, DTBook, NCC (*.opf, *.xml, *.html)");

                    var dlg = new OpenFileDialog
                        {
                            FileName = @"",
                            DefaultExt = @".opf",
                            Multiselect = true,
                            Filter = @"OPF, DTBook, NCC (*.opf, *.xml, *.html)|*.opf;*.xml;*.html",
                            CheckFileExists = false,
                            CheckPathExists = false,
                            AddExtension = true,
                            DereferenceLinks = true,
                            Title =
                                @"Tobi: " +
                                UserInterfaceStrings.EscapeMnemonic(
                                    Tobi_Plugin_Urakawa_Lang.CmdOpenConvert_ShortDesc)
                        };

                    bool? result = false;

                    m_ShellView.DimBackgroundWhile(() => { result = dlg.ShowDialog(); });

                    if (result == false)
                    {
                        return;
                    }

                    if (dlg.FileNames == null || dlg.FileNames.Length < 1)
                    {
                        return;
                    }

                    string ext = Path.GetExtension(dlg.FileNames[0]);

                    if (string.IsNullOrEmpty(ext)
                        || (
                               !ext.Equals(DataProviderFactory.XML_EXTENSION, StringComparison.OrdinalIgnoreCase)
                               &&
                               !ext.Equals(".opf", StringComparison.OrdinalIgnoreCase)
                               &&
                               !ext.Equals(DataProviderFactory.HTML_EXTENSION, StringComparison.OrdinalIgnoreCase)
                           ))
                    {
                        return;
                    }

                    string nowDateTimeStr = DateTime.UtcNow.ToString("MMdd_HHmmss", CultureInfo.InvariantCulture);
                    nowDateTimeStr = FileDataProvider.EliminateForbiddenFileNameCharacters(nowDateTimeStr).Replace(' ', '_');

                    string filename = Path.GetFileName(dlg.FileNames[0]);
                    string outdir = Path.GetDirectoryName(dlg.FileNames[0]);
                    outdir = Path.Combine(outdir, filename.Replace('.', '_') + "_" + nowDateTimeStr);

                    if (Directory.Exists(outdir))
                    {
                        if (!askUserConfirmOverwriteFileFolder(outdir, true, null))
                        {
                            return;
                        }

                        FileDataProvider.TryDeleteDirectory(outdir, false);
                    }

                    string script = "";
                    string inParam = "--i-source";
                    string outParam = "--output"; //"--x-output-dir"
                    string extra = "";

                    string filenames = dlg.FileNames[0];
                    filenames = filenames.Replace('\\', '/');
                    filenames = "file:/" + filenames;
                    filenames = FileDataProvider.UriEncode(filenames);

                    string options = "";

                    bool isNCC = ext.Equals(DataProviderFactory.HTML_EXTENSION, StringComparison.OrdinalIgnoreCase);

                    if (ext.Equals(DataProviderFactory.XML_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    {
                        script = "dtbook-to-epub3";

                        extra = "--x-assert-valid false";
                        options = "<option name=\"assert-valid\">false</option>";

                        if (Settings.Default.Pipeline2OldExe)
                        {
                            filenames = inParam + " \"" + dlg.FileNames[0] + "\"";

                            //filenames = "";

                            //foreach (string fileName in dlg.FileNames)
                            //{
                            //    if (DataProviderFactory.XML_EXTENSION.Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase))
                            //    {
                            //        //filenames += (inParam + " " + "\"" + fileName + "\" ");
                            //        filenames += ("\"" + fileName + "\";");
                            //    }
                            //}

                            //char[] chars = new char[1] { ';' };
                            //filenames = filenames.TrimEnd(chars);


                            //filenames = inParam + " " + filenames;
                        }
                    }
                    else if (ext.Equals(".opf", StringComparison.OrdinalIgnoreCase))
                    {
                        script = "daisy3-to-epub3";

                        extra = "--x-mediaoverlays true";
                        options = "<option name=\"mediaoverlays\">true</option>";

                        if (Settings.Default.Pipeline2OldExe)
                        {
                            filenames = inParam + " \"" + dlg.FileNames[0] + "\"";
                        }
                    }
                    else if (isNCC)
                    {
                        script = "daisy202-to-epub3";
                        inParam = "--x-href";
                        //outParam = "--x-output";

                        extra = "--x-mediaoverlay true --x-compatibility-mode false";
                        options = "<option name=\"mediaoverlay\">true</option><option name=\"compatibility-mode\">false</option>";

                        if (Settings.Default.Pipeline2OldExe)
                        {
                            filenames = inParam + " \"" + dlg.FileNames[0] + "\"";
                        }
                    }

                    bool success = false;
                    string outFile = null;

                    if (Settings.Default.Pipeline2OldExe)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(dlg.FileNames[0]);

                        outFile = Path.Combine(outdir, fileName + ".zip");
                        if (!Directory.Exists(outdir))
                        {
                            Directory.CreateDirectory(outdir);
                        }

                        Func<String, String> checkErrorsOrWarning =
                            (string report) =>
                            {
                                if (report.IndexOf("[DP2] DONE", StringComparison.Ordinal) < 0 || !File.Exists(outFile))
                                {
                                    return "Pipeline fail?";
                                }

#if ENABLE_SHARPZIP
            ZipInputStream zipInputStream = new ZipInputStream(File.OpenRead(outFile));
            ZipEntry zipEntry;
            while ((zipEntry = zipInputStream.GetNextEntry()) != null)
            {
                if (RequestCancellation) return;

                string zipEntryName = Path.GetFileName(zipEntry.Name);
                if (!String.IsNullOrEmpty(zipEntryName)) // || zipEntryName.IndexOf(".ini") >= 0
                {
                    // string unzippedFilePath = Path.Combine(outdir, zipEntryName);
                    string unzippedFilePath = outdir + Path.DirectorySeparatorChar + zipEntry.Name;
                    string unzippedFileDir = Path.GetDirectoryName(unzippedFilePath);
                    if (!Directory.Exists(unzippedFileDir))
                    {
                        FileDataProvider.CreateDirectory(unzippedFileDir);
                    }

                    FileStream fileStream = File.Create(unzippedFilePath);

                    //byte[] data = new byte[2 * 1024]; // 2 KB buffer
                    //int bytesRead = 0;
                    try
                    {
                        const uint BUFFER_SIZE = 1024 * 2; // 2 KB MAX BUFFER
                        StreamUtils.Copy(zipInputStream, 0, fileStream, BUFFER_SIZE);

                        //while ((bytesRead = zipInputStream.Read(data, 0, data.Length)) > 0)
                        //{
                        //    fileStream.Write(data, 0, bytesRead);
                        //}
                    }
                    finally
                    {
                        fileStream.Close();
                    }
                }
            }
            zipInputStream.Close();
#else //ENABLE_SHARPZIP
                                ZipStorer zip = ZipStorer.Open(File.OpenRead(outFile), FileAccess.Read);

                                List<ZipStorer.ZipFileEntry> dir = zip.ReadCentralDir();
                                foreach (ZipStorer.ZipFileEntry entry in dir)
                                {
                                    string unzippedFilePath = outdir + Path.DirectorySeparatorChar + entry.FilenameInZip;
                                    string unzippedFileDir = Path.GetDirectoryName(unzippedFilePath);
                                    if (!Directory.Exists(unzippedFileDir))
                                    {
                                        FileDataProvider.CreateDirectory(unzippedFileDir);
                                    }

                                    zip.ExtractFile(entry, unzippedFilePath);
                                }
                                //zip.Close();
                                zip.Dispose();
#endif //ENABLE_SHARPZIP
                                string outputDirectory = Path.Combine(outdir, isNCC ? "output" : "output-dir");
                                string epubFileName = Path.GetFileNameWithoutExtension(outFile);

                                if (ext.Equals(".opf", StringComparison.OrdinalIgnoreCase))
                                {
                                    epubFileName = "result.epub";
                                }
                                else if (ext.Equals(DataProviderFactory.XML_EXTENSION, StringComparison.OrdinalIgnoreCase))
                                {
                                    epubFileName = epubFileName + DataProviderFactory.EPUB_EXTENSION;
                                }
                                else if (isNCC)
                                {
                                    DirectoryInfo dirInfo = new DirectoryInfo(outputDirectory);
#if NET40
                                    IEnumerable<FileInfo> allFiles = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);
#else
                                    FileInfo[] allFiles = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly);
#endif
                                    foreach (FileInfo fileInfo in allFiles)
                                    {
                                        if (DataProviderFactory.EPUB_EXTENSION.Equals(Path.GetExtension(fileInfo.FullName)))
                                        {
                                            epubFileName = epubFileName + DataProviderFactory.EPUB_EXTENSION;
                                            try
                                            {
                                                string renamed = Path.Combine(outputDirectory, epubFileName);

                                                File.Move(fileInfo.FullName, renamed);
                                                try
                                                {
                                                    File.SetAttributes(renamed, FileAttributes.Normal);
                                                }
                                                catch
                                                {
                                                }
                                                epubFileName = renamed;
                                            }
                                            catch (Exception ex)
                                            {
#if DEBUG
                                                Debugger.Break();
#endif
                                            }

                                            break;
                                        }
                                    }
                                }

                                outFile = Path.Combine(outputDirectory, epubFileName);

                                DebugFix.Assert(File.Exists(outFile));

                                success = true;
                                return null;
                            };

                        try
                        {
                            executeProcess(
                                workingDir,
                                "DAISY Pipeline EXE",
                                //"\"" +
                                pipeline_ExePath
                                //+ "\""
                                ,
                                script + " " +
                                filenames + " " +
                                outParam + " " + "\"" + outFile + "\" " +
                                extra,
                                checkErrorsOrWarning);
                        }
                        catch (Exception ex)
                        {
                            messageBoxText("Oops :(", "Problem running DAISY Pipeline!",
                                           ex.Message + Environment.NewLine + ex.StackTrace);
                        }
                    }
                    else
                    {
                        string outputDir = outdir.Replace('\\', '/');
                        outputDir = "file:/" + outputDir;
                        //outputDir = "/" + outputDir;
                        outputDir = FileDataProvider.UriEncode(outputDir);

                        string jobRequest = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?><jobRequest xmlns=\"http://www.daisy.org/ns/pipeline/data\"><script href=\"" + Resources.baseUri + "/scripts/" + script + "\"/>"
                            + (
                            isNCC
                            ? ("<option name=\"href\">" + filenames + "</option>"
                            //+ "<option name=\"temp-dir\">" + outputDir + "</option>"
                            )
                            : "<input name=\"source\"><item value=\"" + filenames + "\"/></input>"
                            )
                            //+ "<option name=\""
                            //+ (isNCC ? "output" : "output-dir")
                            //+ "\">" + outputDir + "</option>"
                            + options
                            + "</jobRequest>";

                        XmlDocument jobDoc = null;
                        try
                        {
                            jobDoc = Resources.PostJob(jobRequest, null);
                        }
                        catch (Exception ex)
                        {
                            messageBoxText("Pipeline2", "Problem running DAISY Pipeline job!",
                                            jobRequest + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
                        }

                        if (jobDoc == null)
                        {
#if DEBUG
                            Debugger.Break();
#endif
                            m_Logger.Log(String.Format(@"Pipeline2 job failed ({0})", jobRequest),
                                         Category.Debug, Priority.Medium);

                            messageBoxText("Pipeline2", "Pipeline2 job failed!", jobRequest);

                            return;
                        }

                        m_Logger.Log(String.Format(@"Pipeline2 job launched ({0})", jobRequest),
                                     Category.Debug, Priority.Medium);

                        //messageBoxText("Pipeline2", "Pipeline2 job launched :)", jobRequest);

                        MainClass.PrettyPrint(jobDoc);

                        success = false;

                        string id = jobDoc.DocumentElement.GetAttribute("id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            bool cancelled = false;
                            var actionCancelled = (Action)(() =>
                            {
                                cancelled = true;
                            });

                            var actionCompleted = (Action)(() =>
                            {
                            });

                            bool error = m_ShellView.RunModalCancellableProgressTask(true,
            "Pipeline2",
            new PipelineListener(id),
            actionCancelled,
            actionCompleted
            );
                            if (cancelled)
                            {
                                return;
                            }

                            string status = Resources.GetJobStatus(id);

                            success = !cancelled && !error && status == "DONE"; // "ERROR"

                            if (success)
                            {
                                string epubPath = Resources.GetJobOutputEpubFilePath(id);
                                if (!string.IsNullOrEmpty(epubPath))
                                {
                                    DebugFix.Assert(File.Exists(epubPath));

                                    if (isNCC)
                                    {
                                        string fileName =
                                            Path.GetFileNameWithoutExtension(dlg.FileNames[0]);
                                        outFile = Path.Combine(outdir, fileName + DataProviderFactory.EPUB_EXTENSION);
                                    }
                                    else
                                    {
                                        outFile = Path.Combine(outdir, Path.GetFileName(epubPath));
                                    }

                                    if (!Directory.Exists(outdir))
                                    {
                                        Directory.CreateDirectory(outdir);
                                    }
                                    File.Copy(epubPath, outFile);
                                }

                                try
                                {
                                    bool done = Resources.DeleteJob(id);
                                }
                                catch (Exception ex)
                                {
#if DEBUG
                                    Debugger.Break();
#endif
                                    m_Logger.Log(String.Format(@"Pipeline2 DeleteJob ({0})", ex.Message),
                                                 Category.Debug, Priority.Medium);
                                }
                            }
                            else
                            {
                                string msg = null;
                                try
                                {
                                    msg = Resources.GetLog(id);
                                }
                                catch (Exception ex)
                                {
#if DEBUG
                                    Debugger.Break();
#endif
                                    m_Logger.Log(String.Format(@"Pipeline2 GetLog ({0})", ex.Message),
                                                 Category.Debug, Priority.Medium);
                                }

                                messageBoxText("Pipeline2", "Pipeline2 job failed!", !string.IsNullOrEmpty(msg) ? msg : jobRequest);
                            }
                        }
                    }

                    if (!success || !File.Exists(outFile))
                    {
                        if (Directory.Exists(outdir))
                        {
                            m_ShellView.ExecuteShellProcess(outdir);
                        }
                    }
                    else
                    {
                        if (askUser("Import EPUB to create Tobi project?", outFile))
                        {
                            try
                            {
                                OpenFile(outFile);
                            }
                            catch (Exception ex)
                            {
                                m_ShellView.ExecuteShellProcess(outdir);
                                ExceptionHandler.Handle(ex, false, m_ShellView);
                            }
                        }
                        else
                        {
                            checkEpub(outFile, null);

                            m_ShellView.ExecuteShellProcess(outdir);
                        }
                    }
                },
                () => !isAudioRecording,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_OpenConvert)
                );

            m_ShellView.RegisterRichCommand(OpenConvertCommand);
        }

        private class PipelineListener : DualCancellableProgressReporter
        {
            private string m_id = null;
            public PipelineListener(string id)
            {
                m_id = id;
            }

            private int m_percentageProgress = 0;
            public override void DoWork()
            {
                m_percentageProgress = -1;

                int tick = 0;
                string status = null;
                while ((status = Resources.GetJobStatus(m_id)) == "RUNNING" || status == "IDLE")
                {
                    if (RequestCancellation)
                    {
                        return;
                    }

                    Thread.Sleep(2000);
                    tick += 2;

                    reportProgress(m_percentageProgress, "[" + status + "] " + tick);
                }
            }
        }



        private class PipelineAlive : DualCancellableProgressReporter
        {
            private int m_timeout = -1;
            public PipelineAlive(int timeout)
            {
                m_timeout = timeout;
                TimedOut = false;
            }

            public bool TimedOut { get; set; }

            private int m_percentageProgress = 0;
            public override void DoWork()
            {
                Stopwatch stopWatch = null;
                if (m_timeout > 0)
                {
                    stopWatch = Stopwatch.StartNew();
                }

                m_percentageProgress = -1;

                int tick = 0;
                XmlDocument xmlDoc = null;
                while (xmlDoc == null)
                {
                    if (RequestCancellation)
                    {
                        return;
                    }

                    if (stopWatch != null && stopWatch.ElapsedMilliseconds >= m_timeout * 1000)
                    {
                        TimedOut = true;
                        return;
                    }

                    Thread.Sleep(1000);
                    tick++;

                    reportProgress(m_percentageProgress, "Waiting... (" + tick + ")");

                    try
                    {
                        xmlDoc = Resources.Alive();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine(ex1.Message);
                    }
                }
            }
        }


        public void OpenDirectory(string filename)
        {
            if (!askUser("Zip EPUB3?", filename))
            {
                return;
            }

            string mimeTypePath = Path.Combine(filename, "mimetype");
            if (!File.Exists(mimeTypePath))
            {
                StreamWriter mimeTypeWriter = File.CreateText(mimeTypePath);
                try
                {
                    mimeTypeWriter.Write("application/epub+zip");
                }
                finally
                {
                    mimeTypeWriter.Close();
                }
            }

            var dirInfo = new DirectoryInfo(filename).Parent;
            string addon = "";
            int index = 0;
        tryAgain:
            string epubFilePath = Path.Combine(dirInfo.FullName, Path.GetFileName(filename) + addon + DataProviderFactory.EPUB_EXTENSION);
            if (File.Exists(epubFilePath))
            {
                addon = "_" + index;
                goto tryAgain;
            }
            Epub3_Export.ZipEpub(epubFilePath, filename);

            if (File.Exists(epubFilePath))
            {
                checkEpub(epubFilePath, null);
            }
        }

        public void TryOpenFile(string filename)
        {
            if (OpenCommand.CanExecute() && File.Exists(filename))
            {
                OpenFile(filename);
            }
            else if (Directory.Exists(filename))
            {
                OpenDirectory(filename);
            }
        }

        public bool OpenFile(string filename)
        {
            return OpenFile(filename, true);
        }

        public bool OpenFile(string filename, bool doShowXukSpineCommand)
        {
            m_Logger.Log(String.Format(@"UrakawaSession.openFile({0})", filename), Category.Debug, Priority.Medium);

            var fileUri = new Uri(filename, UriKind.Absolute);

            if (checkWarningFilePathLength(fileUri.LocalPath))
            {
                return false;
            }

            AddRecentFile(fileUri);

            if (!File.Exists(fileUri.LocalPath)
                || !fileUri.IsFile)
            {
                var label = new TextBlock
                {
                    Text = Tobi_Plugin_Urakawa_Lang.CannotOpenLocalFile_,
                    Margin = new Thickness(8, 0, 8, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Focusable = true,
                    TextWrapping = TextWrapping.Wrap
                };

                var iconProvider = new ScalableGreyableImageProvider(m_ShellView.LoadTangoIcon("dialog-warning"), m_ShellView.MagnificationLevel);

                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
                panel.Children.Add(iconProvider.IconLarge);
                panel.Children.Add(label);

                var details = new TextBoxReadOnlyCaretVisible
                {
                    FocusVisualStyle = (Style)Application.Current.Resources["MyFocusVisualStyle"],

                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6),
                    TextReadOnly = fileUri.ToString()
                };

                var windowPopup = new PopupModalWindow(m_ShellView,
                                                       UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.CannotOpenLocalFile),
                                                       panel,
                                                       PopupModalWindow.DialogButtonsSet.Close,
                                                       PopupModalWindow.DialogButton.Close,
                                                       true, 300, 170, details, 40, null);

                windowPopup.ShowModal();

                return false;
            }

            // Closing is REQUIRED ! 
            PopupModalWindow.DialogButton button = CheckSaveDirtyAndClose(PopupModalWindow.DialogButtonsSet.YesNoCancel, Tobi_Plugin_Urakawa_Lang.UrakawaSession_OpenFile);
            if (PopupModalWindow.IsButtonEscCancel(button))
            {
                return false;
            }

            string ext = Path.GetExtension(fileUri.ToString());
            if (
                OpenXukAction.XUK_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                ||
                OpenXukAction.XUK_SPINE_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                )
            {
                //todo: should we implement HTTP open ?
                if (!fileUri.IsFile)
                    throw new InvalidUriException("The URI to open must point to a local file! " + Environment.NewLine + fileUri.ToString());

                //fileUri.Scheme.ToLower() == "file"
                DocumentFilePath = fileUri.IsFile ? fileUri.LocalPath : filename;

                m_Logger.Log(String.Format(@"UrakawaSession.openFile(XUK) [{0}]", DocumentFilePath), Category.Debug, Priority.Medium);

                if (!File.Exists(DocumentFilePath))
                    throw new InvalidUriException("The import URI must point to an existing file! " + Environment.NewLine + fileUri.ToString());

                var project = new Project();

                //var backWorker = new BackgroundWorker
                //    {
                //        WorkerSupportsCancellation = true,
                //        WorkerReportsProgress = true
                //    };

                //var uri = new Uri(DocumentFilePath, UriKind.Absolute);
                //DocumentProject.OpenXuk(uri);

                var action = new OpenXukAction(project, fileUri)
                {
                    ShortDescription = Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_ShortDesc,
                    LongDescription = Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_LongDesc
                };

                bool cancelled = false;

                bool error = m_ShellView.RunModalCancellableProgressTask(true,
                    Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_ShortDesc, action,
                    () =>
                    {
                        cancelled = true;

                        DocumentFilePath = null;
                        DocumentProject = null;

                        //backWorker.CancelAsync();
                    },
                    () =>
                    {
                        cancelled = false;

                        if (project.Presentations.Count == 0)
                        {
                            Debug.Fail("Project does not contain a Presentation !" + Environment.NewLine +
                                       fileUri.ToString());
                            //workException = new XukException()
                        }
                        else
                        {
                            DocumentProject = project;
                        }
                    }
                    );

                if (cancelled)
                {
                    //DebugFix.Assert(!report);
                    return false;
                }

                if (DocumentProject != null)
                {
                    //m_Logger.Log(@"-- PublishEvent [ProjectLoadedEvent] UrakawaSession.OpenFile", Category.Debug, Priority.Medium);

                    if (m_EventAggregator != null)
                    {
                        m_EventAggregator.GetEvent<ProjectLoadedEvent>().Publish(DocumentProject);
                    }

                    if (IsXukSpine
                        //String.Equals(ext, OpenXukAction.XUK_SPINE_EXTENSION, StringComparison.OrdinalIgnoreCase)
                        )
                    {
                        parseXukSpine(DocumentFilePath, DocumentProject, null);

                        if (doShowXukSpineCommand
                            && ShowXukSpineCommand.CanExecute())
                        {
                            Application.Current.MainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => ShowXukSpineCommand.Execute()));
                        }
                    }
                    else
                    {
                        if (IsSplitMaster)
                        {
                            messageBoxText(Tobi_Plugin_Urakawa_Lang.Menu_SplitMergeProject, Tobi_Plugin_Urakawa_Lang.IsSplitMaster, null);
                        }
                        else if (IsSplitSub)
                        {
                            messageBoxText(Tobi_Plugin_Urakawa_Lang.Menu_SplitMergeProject, Tobi_Plugin_Urakawa_Lang.IsSplitSub, null);
                        }


                        //var treeNode = TreeNode.EnsureTreeNodeHasNoSignificantTextOnlySiblings(false, DocumentProject.Presentations.Get(0).RootNode, null);

                        TreeNode treeNode = null;
                        var lname = DocumentProject.Presentations.Get(0).RootNode.GetXmlElementLocalName();
                        if (lname != null && (lname.Equals("math", StringComparison.OrdinalIgnoreCase) ||
                            lname.Equals("svg", StringComparison.OrdinalIgnoreCase)))
                        {
                            treeNode = DocumentProject.Presentations.Get(0).RootNode;
                        }
                        else
                        {
                            treeNode = TreeNode.NavigateInsideSignificantText(DocumentProject.Presentations.Get(0).RootNode);
                        }

                        if (treeNode != null)
                        {
                            //m_Logger.Log(@"-- PublishEvent [TreeNodeSelectedEvent] DocumentPaneView.OnFlowDocumentLoaded", Category.Debug, Priority.Medium);

                            PerformTreeNodeSelection(treeNode);
                            //m_EventAggregator.GetEvent<TreeNodeSelectedEvent>().Publish(treeNode);
                        }
                        else
                        {
                            PerformTreeNodeSelection(DocumentProject.Presentations.Get(0).RootNode);
                        }

                        tryParseXukSpine(DocumentFilePath);

                        CommandManager.InvalidateRequerySuggested();
                    }

                    return true;
                }




                //var progressBar = new ProgressBar
                //{
                //    IsIndeterminate = false,
                //    Height = 18,
                //    HorizontalAlignment = HorizontalAlignment.Stretch,
                //    Minimum = 0,
                //    Maximum = 100,
                //    Value = 0
                //};

                //var label = new TextBlock
                //{
                //    Text = action.ShortDescription,
                //    Margin = new Thickness(0, 0, 0, 8),
                //    HorizontalAlignment = HorizontalAlignment.Left,
                //    VerticalAlignment = VerticalAlignment.Top,
                //    Focusable = true,
                //};
                //var panel = new StackPanel
                //{
                //    Orientation = Orientation.Vertical,
                //    HorizontalAlignment = HorizontalAlignment.Stretch,
                //    VerticalAlignment = VerticalAlignment.Center,
                //};

                //panel.Children.Add(label);
                //panel.Children.Add(progressBar);

                //var details = new TextBoxReadOnlyCaretVisible(action.LongDescription);

                //var windowPopup = new PopupModalWindow(m_ShellView,
                //                                       UserInterfaceStrings.EscapeMnemonic(
                //                                           Tobi_Plugin_Urakawa_Lang.RunningTask),
                //                                       panel,
                //                                       PopupModalWindow.DialogButtonsSet.Cancel,
                //                                       PopupModalWindow.DialogButton.Cancel,
                //                                       false, 500, 150, details, 80);


                //Exception workException = null;
                //backWorker.DoWork += delegate(object s, DoWorkEventArgs args)
                //{
                //    //var dummy = (string)args.Argument;

                //    if (backWorker.CancellationPending)
                //    {
                //        args.Cancel = true;
                //        return;
                //    }

                //    action.Execute();

                //    args.Result = @"dummy result";
                //};

                //backWorker.ProgressChanged += delegate(object s, ProgressChangedEventArgs args)
                //{
                //    progressBar.Value = args.ProgressPercentage;
                //};

                //backWorker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
                //{
                //    workException = args.Error;

                //    if (cancelFlag)
                //    {
                //        DocumentFilePath = null;
                //        DocumentProject = null;
                //    }
                //    else if (args.Cancelled)
                //    {
                //        DocumentFilePath = null;
                //        DocumentProject = null;
                //        windowPopup.ForceClose(PopupModalWindow.DialogButton.Cancel);
                //    }
                //    else if (workException != null)
                //    {
                //        windowPopup.ForceClose(PopupModalWindow.DialogButton.ESC);
                //    }
                //    else
                //    {
                //        if (project.Presentations.Count == 0)
                //        {
                //            workException = new XukException("Project does not contain a Presentation !" + Environment.NewLine + fileUri.ToString());
                //        }
                //        else
                //            DocumentProject = project;
                //        windowPopup.ForceClose(PopupModalWindow.DialogButton.ESC);
                //    }


                //    //var result = (string)args.Result;

                //    backWorker = null;
                //};

                //backWorker.RunWorkerAsync(@"dummy arg");
                //windowPopup.ShowModal();

                //if (workException != null)
                //{
                //    throw workException;
                //}

                //if (windowPopup.ClickedDialogButton == PopupModalWindow.DialogButton.Cancel)
                //{
                //    cancelFlag = true;
                //    return false;
                //}
            }
            else if (".mml".Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                string mathML = File.ReadAllText(fileUri.LocalPath, Encoding.UTF8);
                string svgFileOutput = fileUri.LocalPath + DataProviderFactory.IMAGE_SVG_EXTENSION;

                string svg = Convert_MathML_to_SVG(mathML, svgFileOutput);

                m_ShellView.ExecuteShellProcess(Path.GetDirectoryName(fileUri.LocalPath));
            }
            else if (".obi".Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                initSaxonXslt_Obi_Tobi_XUK();

                string outputFileName = null;
                lock (LOCK)
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(fileUri.LocalPath);

                    XdmNode inputNode = m_SaxonProcessor.NewDocumentBuilder().Build(xmlDoc);
                    m_SaxonXslt_Obi_Tobi_XUK.InitialContextNode = inputNode;

                    outputFileName = fileUri.LocalPath + OpenXukAction.XUK_EXTENSION;
                    using (var streamWriter = new StreamWriter(outputFileName, false, Encoding.UTF8))
                    {
                        var xmlWriter = new XmlTextWriter(streamWriter);

                        var dest = new TextWriterDestination(xmlWriter);
                        m_SaxonXslt_Obi_Tobi_XUK.Run(dest);
                    }
                }

                OpenFile(outputFileName);
            }
            else if (
                DataProviderFactory.XHTML_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                || DataProviderFactory.HTML_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                || DataProviderFactory.XML_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                || ".opf".Equals(ext, StringComparison.OrdinalIgnoreCase)
                || DataProviderFactory.EPUB_EXTENSION.Equals(ext, StringComparison.OrdinalIgnoreCase)
                )
            {
                //todo: should we implement HTTP import ?
                if (!fileUri.IsFile)
                    throw new InvalidUriException("The import URI must point to a local file!" + Environment.NewLine + fileUri.ToString());

                //fileUri.Scheme.ToLower() == "file"
                DocumentFilePath = fileUri.LocalPath; //fileUri.IsFile ? fileUri.LocalPath : filename;

                if (!File.Exists(DocumentFilePath))
                    throw new InvalidUriException("The import URI must point to an existing file! " + Environment.NewLine + fileUri.ToString());

                if (!doImport())
                {
                    return false;
                }
            }

            return false;
        }

        private bool parseXukSpine(string projectPath, Project project, string xukFileToMatch)
        {
            //if (string.IsNullOrEmpty(xukFileToMatch))
            //{
            //    MessageBox.Show("EPUB support is experimental and incomplete, please use with caution!");
            //}

            string rootDir = Path.GetDirectoryName(projectPath);
            //string rootDir = Directory.GetParent(dir).Name;

            XukSpineItems = new ObservableCollection<XukSpineItemData>();

            Presentation presentation = project.Presentations.Get(0);

            foreach (var treeNode in presentation.RootNode.Children.ContentsAs_Enumerable)
            {
                TextMedia txtMedia = treeNode.GetTextMedia() as TextMedia;
                if (txtMedia == null) continue;
                string path = txtMedia.Text;

                XmlProperty xmlProp = treeNode.GetXmlProperty();
                if (xmlProp == null) continue;

                string name = treeNode.GetXmlElementLocalName();
                if (name != "metadata") continue;

                string title = null;
                string xukFileName = null;
                bool hasXuk = false;
                foreach (var xmlAttr in xmlProp.Attributes.ContentsAs_Enumerable)
                {
                    if (xmlAttr.LocalName == "xuk" && xmlAttr.Value == "true")
                    {
                        hasXuk = true;
                    }

                    if (xmlAttr.LocalName == "title")
                    {
                        title = xmlAttr.Value;
                    }

                    if (xmlAttr.LocalName == "xukFileName")
                    {
                        xukFileName = xmlAttr.Value;
                    }
                }

                if (!hasXuk) continue;

                //string title_ = Daisy3_Import.GetTitle(presentation);
                //DebugFix.Assert(title_ == title);

                string fullXukPath = null;
                if (!string.IsNullOrEmpty(xukFileName))
                {
                    fullXukPath = Path.Combine(rootDir, xukFileName);
                }
                else
                {
                    //old project format
                    fullXukPath = Daisy3_Import.GetXukFilePath_SpineItem(rootDir, path, title, -1);
                }

                if (!File.Exists(fullXukPath))
                {
#if DEBUG
                    Debugger.Break();
#endif //DEBUG
                    continue;
                }

                if (!string.IsNullOrEmpty(xukFileToMatch))
                {
                    string fileOnly = Path.GetFileName(fullXukPath);

                    if (xukFileToMatch == fileOnly)
                    {
                        return true;
                    }
                }
                else
                {
                    Uri uri = new Uri(fullXukPath, UriKind.Absolute);
                    XukSpineItems.Add(new XukSpineItemData(uri, title));
                }
            }

            return false;
        }

        private void tryParseXukSpine(string currentProjectPath)
        {
            try
            {
                string fileOnly = Path.GetFileName(currentProjectPath);

                string dir = Path.GetDirectoryName(currentProjectPath);
                string[] files = Directory.GetFiles(dir, "*.xukspine"
#if NET40
, SearchOption.TopDirectoryOnly
#endif
);
                if (files == null || files.Length <= 0)
                {
                    dir = Path.GetDirectoryName(dir);
                    files = Directory.GetFiles(dir, "*.xukspine"
#if NET40
, SearchOption.TopDirectoryOnly
#endif
);
                }

                foreach (var projectPath in files)
                {
                    Uri uri = new Uri(projectPath, UriKind.Absolute);

                    var project = new Project();

                    var action = new OpenXukAction(project, uri)
                        {
                            ShortDescription = Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_ShortDesc,
                            LongDescription = Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_LongDesc
                        };

                    bool cancelled = false;

                    bool error = m_ShellView.RunModalCancellableProgressTask(true,
                        Tobi_Plugin_Urakawa_Lang.UrakawaOpenAction_ShortDesc, action,
                        () =>
                        {
                            cancelled = true;
                            project = null;
                        },
                        () =>
                        {
                            cancelled = false;

                            if (project.Presentations.Count == 0)
                            {
                                Debug.Fail(
                                    "Project does not contain a Presentation !" +
                                    Environment.NewLine +
                                    uri.ToString());
                                //workException = new XukException()
                            }
                        }
                        );

                    if (cancelled)
                    {
                        //DebugFix.Assert(!report);
                        return;
                    }

                    bool ok = parseXukSpine(projectPath, project, fileOnly);
                    if (ok)
                    {
                        XukSpineProjectPath = projectPath;
                        parseXukSpine(projectPath, project, null);
                        return;
                    }
                }
            }
            finally
            {
                // XukStrings maintains a pointer to the last-created Project instance!
                //XukStrings.RelocateProjectReference(DocumentProject);
            }
        }

        private readonly Object LOCK = new object();

        private Processor m_SaxonProcessor;

        private void initSaxonXslt(string xslFilePath, out XsltTransformer xlstTransformer)
        {
            if (m_SaxonProcessor == null)
            {
                m_SaxonProcessor = new Processor
                {
                    XmlResolver = new LocalXmlUrlResolver(false)
                };
            }

            var stream = new FileStream(xslFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            var baseUri = new Uri("file:///" + xslFilePath);

            XmlReaderSettings settings = XmlReaderWriterHelper.GetDefaultXmlReaderConfiguration(false, false, false);

            var xmlReader = XmlReader.Create(stream, settings, baseUri.ToString());

            //TextReader xmlReader = new StreamReader(xslFilePath, Encoding.UTF8);

            XsltCompiler compiler = m_SaxonProcessor.NewXsltCompiler();
            compiler.BaseUri = baseUri;
            compiler.XmlResolver = new LocalXmlUrlResolver(false);

            try
            {
                XsltExecutable exe = compiler.Compile(xmlReader);
                xlstTransformer = exe.Load();
            }
            catch (Exception ex)
            {
                xmlReader.Close();

                consoleWrite(ex);

                foreach (StaticError error in compiler.ErrorList)
                {
                    Console.WriteLine("At line " + error.LineNumber + ": " + error.Message);
                }
                throw;
            }

            xmlReader.Close();

            xlstTransformer.InputXmlResolver = new LocalXmlUrlResolver(false);
            xlstTransformer.MessageListener = new MyMessageListener();
            //xlstTransformer.BaseOutputUri = compiler.BaseUri;
        }

        private XsltTransformer m_SaxonXslt_Obi_Tobi_XUK;
        private void initSaxonXslt_Obi_Tobi_XUK()
        {
            lock (LOCK)
            {
                if (m_SaxonXslt_Obi_Tobi_XUK == null)
                {
                    string workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string xslFilePath = Path.Combine(workingDirectory, "Obi-to-XUK2.xsl");

                    initSaxonXslt(xslFilePath, out m_SaxonXslt_Obi_Tobi_XUK);
                }
            }
        }

        private XsltTransformer m_SaxonXslt_MathML_SVG;
        private void initSaxonXslt_MathML_SVG()
        {
            lock (LOCK)
            {
                if (m_SaxonXslt_MathML_SVG == null)
                {
                    //http://www.cs.duke.edu/courses/fall08/cps116/docs/saxon/samples/cs/Examples.cs

                    string workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string xslt_dir = Path.Combine(workingDirectory, "pmml2svg_XSLT2");

                    //string xslFilePath1 = Path.Combine(xslt_dir, "fontMetrics.xsl");

                    //string xslFilePath2 = Path.Combine(xslt_dir, "formattingMode.xsl");

                    //string xslFilePath3 = Path.Combine(xslt_dir, "drawingMode.xsl");


                    string xslFilePath = Path.Combine(xslt_dir, "pmml2svg.xsl");

                    initSaxonXslt(xslFilePath, out m_SaxonXslt_MathML_SVG);
                }
            }
        }

        private void consoleWrite(Exception ex)
        {
            if (ex.Message != null)
            {
                Console.WriteLine(ex.Message);
            }
            if (ex.StackTrace != null)
            {
                Console.WriteLine(ex.StackTrace);
            }
            if (ex.InnerException != null)
            {
                consoleWrite(ex.InnerException);
            }
        }

        public string Convert_MathML_to_SVG(string mathML, string svgFileOutput)
        {
            initSaxonXslt_MathML_SVG();

            lock (LOCK)
            {
                var source = new XmlTextReader(new StringReader(mathML));
                //var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceString));

                var xmlDoc = new XmlDocument();
                xmlDoc.Load(source);

                source.Close();

                DocumentBuilder builder = m_SaxonProcessor.NewDocumentBuilder();
                //builder.BaseUri = baseUri;
                //builder.XmlResolver = new LocalXmlUrlResolver(false);
                XdmNode inputNode = builder.Build(xmlDoc);
                m_SaxonXslt_MathML_SVG.InitialContextNode = inputNode;


                if (!string.IsNullOrEmpty(svgFileOutput))
                {
                    using (var streamWriter = new StreamWriter(svgFileOutput, false, Encoding.UTF8))
                    {
                        var xmlWriter = new XmlTextWriter(streamWriter);

                        var dest = new TextWriterDestination(xmlWriter);
                        m_SaxonXslt_MathML_SVG.Run(dest);
                    }

                    string src = File.ReadAllText(svgFileOutput);
                    return src;
                }
                else
                {
                    var dest = new XdmDestination();
                    m_SaxonXslt_MathML_SVG.Run(dest);
                    string src = dest.XdmNode.OuterXml;
                    return src;
                }
            }
        }
    }

    public class MyMessageListener : IMessageListener
    {
        public void Message(XdmNode content, bool terminate, IXmlLocation location)
        {
            Console.Out.WriteLine("MESSAGE terminate=" + (terminate ? "yes" : "no") + " at " + DateTime.Now);
            Console.Out.WriteLine("From instruction at line " + location.LineNumber + " of " + location.BaseUri);
            Console.Out.WriteLine(">>" + content.StringValue);
        }
    }
}