﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;

// usage : command infile outfile

namespace TextConverter
{
    class Program
    {
        public static void Main(string[] args)
        {
            if (args == null)
            {
                Console.WriteLine("usage : <command> <arg1 mainframe input filename> <arg2 postscript output filename> <optional pagesize>");
                Environment.Exit(1);
            }

            if (args.Length < 2)
            {
                Console.WriteLine("You need to provide an input and output filename");
                Environment.Exit(1);
            }

            // prep the Encapsulated PS files generated by Adobe (.eps)
            string fuleps = ConfigurationManager.AppSettings["fuleps"];
            string buleps = ConfigurationManager.AppSettings["buleps"];
            string fulps = ConfigurationManager.AppSettings["fulps"];
            string bulps = ConfigurationManager.AppSettings["bulps"];

            UnderLayPrep(fuleps, buleps, fulps, bulps);

            string InFile = args[0];
            string OutFile = args[1];
            int[] pagesize = { Convert.ToInt32(ConfigurationManager.AppSettings["xpoints"]), Convert.ToInt32(ConfigurationManager.AppSettings["ypoints"]) };
            string jobfont = ConfigurationManager.AppSettings["jobfont"];
            int jobfontsize = Convert.ToInt32(ConfigurationManager.AppSettings["jobfontsize"]);
            int linecount = Convert.ToInt32(ConfigurationManager.AppSettings["linecount"]);

            int[] linebreakchars = { Convert.ToInt32(ConfigurationManager.AppSettings["linechar0"], 16), Convert.ToInt32(ConfigurationManager.AppSettings["linechar1"], 16) };

            int[] pagebreakchars = new int[2];
            if (ConfigurationManager.AppSettings["pagechar0"] != "none" || ConfigurationManager.AppSettings["pagechar1"] != "none")
            {
                pagebreakchars[0] = Convert.ToInt32(ConfigurationManager.AppSettings["pagechar0"], 16);
                pagebreakchars[1] = Convert.ToInt32(ConfigurationManager.AppSettings["pagechar1"], 16);
            }

            PrintFile MemoryFile = new PrintFile(StorePage(InFile, linebreakchars, pagebreakchars, linecount));

            if (MemoryFile.Pages == null)
            {
                Console.WriteLine("File returned null, something went wrong");
                Environment.Exit(1);
            }

            StringBuilder PSHeader = PostScriptHeader(MemoryFile.Pages.Count, pagesize, jobfont, jobfontsize, Path.GetFileName(InFile), Path.GetFileName(OutFile));

            int xstart = Convert.ToInt32(ConfigurationManager.AppSettings["xstart"]);
            int ystart = Convert.ToInt32(ConfigurationManager.AppSettings["ystart"]);
            int linespacing = Convert.ToInt32(ConfigurationManager.AppSettings["linespacing"]);

            // underlay must be none if not in use
            PostScriptEmitter(PSHeader, MemoryFile, OutFile, xstart, ystart, linespacing, fulps, bulps);
        }


        public static List<Page> StorePage(string infile, int[] linebreakchars, int[] pagebreakchars, int linecount)
        {
            int lastbyte = 0;
            int bytein = 0;
            int numlines = 0;

            var lines = new List<StringBuilder>();
            var linebuffer = new StringBuilder();

            var thispage = new Page();

            var pages = new List<Page>();
            var thisfile = new PrintFile();

            try
            {
                using (StreamReader reader = new StreamReader(infile))
                {
                    while (reader.EndOfStream != true)
                    {
                        bytein = reader.Read();

                        // while not the end of a page, build the lines on the page list

                        // Console.WriteLine("value of bytein HEX : {0:x2}  DEC : {1}  CHAR : {2}", bytein, bytein, (char) bytein);
                        // Console.WriteLine("value of bytein HEX : {0:x2}  DEC : {1}", bytein, bytein);

                        if (linecount == 0 && pagebreakchars != null) // we are not using line count
                        {
                            // is it the end of a line
                            if ((bytein == linebreakchars[0] && reader.Peek() == linebreakchars[1]) || (lastbyte != linebreakchars[0] && bytein == linebreakchars[1]))
                            {
                                lines.Add(linebuffer);

                                if (bytein == linebreakchars[0] && reader.Peek() == linebreakchars[1])
                                {
                                    // move past the 0x0D
                                    // bytein = reader.Read();
                                    // move past the 0x0A
                                    bytein = reader.Read();
                                }

                                // finalize the values of the stringbuffer
                                lastbyte = bytein;
                                linebuffer = new StringBuilder();
                            }

                            else if (bytein == pagebreakchars[0] && reader.Peek() == pagebreakchars[1])
                            {
                                thispage = new Page(lines);
                                pages.Add(thispage);
                                lines = new List<StringBuilder>();
                                bytein = reader.Read();
                                lastbyte = bytein;
                            }

                            // is not the end of a line or page, build linebuffer
                            else
                            {
                                linebuffer.Append((char)bytein);
                                lastbyte = bytein;
                            }
                        }
                        else // we are using linecount
                        {
                            if ((numlines < linecount) && (bytein == linebreakchars[0] && reader.Peek() == linebreakchars[1]) || (lastbyte != linebreakchars[0] && bytein == linebreakchars[1]))
                            {
                                lines.Add(linebuffer);
                                numlines++;

                                if (bytein == linebreakchars[0] && reader.Peek() == linebreakchars[1])
                                {
                                    // move past the 0x0D
                                    // bytein = reader.Read();
                                    // move past the 0x0A
                                    bytein = reader.Read();
                                }

                                // finalize the values of the stringbuffer
                                lastbyte = bytein;
                                linebuffer = new StringBuilder();

                                if (numlines == linecount)
                                {
                                    thispage = new Page(lines);
                                    pages.Add(thispage);
                                    lines = new List<StringBuilder>();
                                    numlines = 0;
                                }
                            }

                            else
                            {
                                linebuffer.Append((char)bytein);
                                lastbyte = bytein;
                            }
                        }
                    }

                    // did the file end in a 0C ? if so we don't want to add an extra page
                    // if it is not a 0C then the file ends otherwise, and we want one more page
                    lines.Add(linebuffer);
                    linebuffer = new StringBuilder();
                    thispage = new Page(lines);
                    pages.Add(thispage);
                    return pages;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading file : {0}", e.Message);
                return pages;
            }
        }

        public static void PostScriptEmitter(StringBuilder PSheader, PrintFile memoryfile, string psoutputfile,
            int xstart, int ystart, int linespacing, string frontul = "none", string backul = "none")
        {
            try
            {
                if (!File.Exists(psoutputfile))
                {
                    using (StreamWriter psout = File.CreateText(psoutputfile))
                    {
                        psout.WriteLine("%!PS-Mark-1.0");
                        if (frontul != "none")
                        {
                            // add underlay
                            if (File.Exists(frontul))
                            {
                                string fulbuffer = File.ReadAllText(frontul);
                                psout.Write(fulbuffer);
                            }
                        }
                        if (backul != "none")
                        {
                            // add underlay
                            if (File.Exists(backul))
                            {
                                string bulbuffer = File.ReadAllText(backul);
                                psout.Write(bulbuffer);
                            }
                        }
                        psout.WriteLine(PSheader);

                        foreach (Page page in memoryfile.Pages)
                        {
                            int ystartval = ystart;
                            foreach (StringBuilder line in page.Lines)
                            {
                                psout.WriteLine(String.Format("{0} {1} moveto", xstart, ystartval));
                                psout.WriteLine(String.Format("({0}) show", line));

                                ystartval = ystartval - linespacing;
                            }
                            // TODO if there are underlays put them in here
                            if (frontul != "none")
                            {
                                // add front underlay
                                psout.WriteLine(String.Format("save"));
                                psout.WriteLine(String.Format("  1 1 scale"));
                                psout.WriteLine(String.Format("  1 1 translate"));
                                psout.WriteLine(String.Format("FPForm execform"));
                                psout.WriteLine(String.Format("restore"));
                            }
                            if (backul != "none")
                            {
                                psout.WriteLine(String.Format("showpage"));
                                // add back underlay
                                psout.WriteLine(String.Format("save"));
                                psout.WriteLine(String.Format("  1 1 scale"));
                                psout.WriteLine(String.Format("  1 1 translate"));
                                psout.WriteLine(String.Format("BPForm execform"));
                                psout.WriteLine(String.Format("restore"));
                                psout.WriteLine(String.Format("showpage"));
                            }
                            else
                            {
                                psout.WriteLine(String.Format("showpage"));
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("File Exists !");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("There was an error : {0}", e);
            }
        }
        public static StringBuilder PostScriptHeader(int pages, int[] pagesize, string jobfont, int jobfontsize,
            string inputfilename, string outputfilename)
        {
            StringBuilder PSHeader = new StringBuilder();

            //  build control page information
            PSHeader.AppendLine("%% Mark McCall PS version 1.0");
            PSHeader.AppendLine(String.Format("%% file should have {0} pages", pages));
            PSHeader.AppendLine("%% ");
            PSHeader.AppendLine("%% ");
            PSHeader.AppendLine("%% ");
            PSHeader.AppendLine("%%BeginPageSetup");
            PSHeader.AppendLine("/Courier findfont");
            PSHeader.AppendLine("16 scalefont setfont");
            PSHeader.AppendLine("%%EndPageSetup");
            PSHeader.AppendLine("%% ");
            PSHeader.AppendLine("35 570 moveto");
            PSHeader.AppendLine("(Mainframe Output to Postscript) show");
            PSHeader.AppendLine("35 550 moveto");
            PSHeader.AppendLine(String.Format("(Sheets in Job : {0}) show", pages));
            PSHeader.AppendLine("35 530 moveto");
            PSHeader.AppendLine(String.Format("(Input Filename : {0}) show", inputfilename));
            PSHeader.AppendLine("35 510 moveto");
            PSHeader.AppendLine(String.Format("(Output Filename : {0}) show", outputfilename));

            PSHeader.AppendLine("showpage");

            //build job header
            PSHeader.AppendLine("%%BeginPageSetup");
            PSHeader.AppendLine(String.Format("<</PageSize [{0} {1}]>> setpagedevice", pagesize[0], pagesize[1]));
            PSHeader.AppendLine(String.Format("/{0} findfont", jobfont));
            PSHeader.AppendLine(String.Format("{0} scalefont setfont", jobfontsize));

            if (ConfigurationManager.AppSettings["plex"] == "duplex")
            {
                PSHeader.AppendLine("<< /Duplex true >> setpagedevice");
            }

            PSHeader.AppendLine("%%EndPageSetup");
            PSHeader.AppendLine(" ");
            PSHeader.AppendLine(" ");

            return PSHeader;
        }

        public static void UnderLayPrep(string FrontUnderlay = "none", string BackUnderlay = "none", string fulps = "none", string bulps = "none")
        {
            StringBuilder FrontULheader = new StringBuilder();
            StringBuilder FrontULtail = new StringBuilder();
            StringBuilder BackULheader = new StringBuilder();
            StringBuilder BackULtail = new StringBuilder();

            // TODO : take in underlay and append appropriate postscript wrapper

            if (FrontUnderlay != "none")
            {
                if (File.Exists(FrontUnderlay))
                {
                    // then there is a front underlay
                    FrontULheader.AppendLine("/FPData");
                    FrontULheader.AppendLine("currentfile");
                    FrontULheader.AppendLine("<< /Filter /SubFileDecode");
                    FrontULheader.AppendLine("   /DecodeParms << /EODString (*EOD*) >>");
                    FrontULheader.AppendLine(">> /ReusableStreamDecode filter");

                    // EPS file gets inserted here
                    string fulbuffer = String.Empty;
                    fulbuffer = File.ReadAllText(FrontUnderlay);

                    FrontULtail.AppendLine("*EOD*");
                    FrontULtail.AppendLine("def");
                    FrontULtail.AppendLine("/FPForm");
                    FrontULtail.AppendLine("<< /FormType 1");
                    FrontULtail.AppendLine("   /BBox [0 0 612 792]");
                    FrontULtail.AppendLine("   /Matrix [ 1 0 0 1 0 0]");
                    FrontULtail.AppendLine("   /PaintProc");
                    FrontULtail.AppendLine("   { pop");
                    FrontULtail.AppendLine("       /ostate save def");
                    FrontULtail.AppendLine("         /showpage {} def");
                    FrontULtail.AppendLine("         /setpagedevice /pop load def");
                    FrontULtail.AppendLine("         FPData 0 setfileposition FPData cvx exec");
                    FrontULtail.AppendLine("       ostate restore");
                    FrontULtail.AppendLine("   } bind");
                    FrontULtail.AppendLine(">> def");

                    File.WriteAllText(fulps, FrontULheader + fulbuffer + FrontULtail);
                }
            }

            if (BackUnderlay != "none")
            {
                if (File.Exists(BackUnderlay))
                {
                    // then there is a back underlay
                    BackULheader.AppendLine("/BPData");
                    BackULheader.AppendLine("currentfile");
                    BackULheader.AppendLine("<< /Filter /SubFileDecode");
                    BackULheader.AppendLine("   /DecodeParms << /EODString (*EOD*) >>");
                    BackULheader.AppendLine(">> /ReusableStreamDecode filter");

                    // EPS file gets inserted here
                    string bulbuffer = String.Empty;
                    bulbuffer = File.ReadAllText(BackUnderlay);

                    BackULtail.AppendLine("*EOD*");
                    BackULtail.AppendLine("def");
                    BackULtail.AppendLine("/BPForm");
                    BackULtail.AppendLine("<< /FormType 1");
                    BackULtail.AppendLine("   /BBox [0 0 612 792]");
                    BackULtail.AppendLine("   /Matrix [ 1 0 0 1 0 0]");
                    BackULtail.AppendLine("   /PaintProc");
                    BackULtail.AppendLine("   { pop");
                    BackULtail.AppendLine("       /ostate save def");
                    BackULtail.AppendLine("         /showpage {} def");
                    BackULtail.AppendLine("         /setpagedevice /pop load def");
                    BackULtail.AppendLine("         BPData 0 setfileposition BPData cvx exec");
                    BackULtail.AppendLine("       ostate restore");
                    BackULtail.AppendLine("   } bind");
                    BackULtail.AppendLine(">> def");

                    File.WriteAllText(bulps, BackULheader + bulbuffer + BackULtail);
                }
            }
        }
    }
}
