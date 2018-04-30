using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using Microsoft.CSharp;

namespace NSLaserCfg {
    static class InferenceUtil {
        internal static bool writeElements = true;
        static CodeTypeReference ctrSerialAttr;

        internal static void inferSchema(string fileName) {
            XmlSchemaInference xsi;
            XmlReaderSettings xrs;

            //writeElements = false;
            try {
                ctrSerialAttr = new CodeTypeReference(writeElements ? "XmlElement" : "XmlAttribute");
                xsi = new XmlSchemaInference();
                xrs = new XmlReaderSettings();
                using (XmlReader xr = XmlReader.Create(fileName, xrs)) {
                    handleSchemaInference(xsi.InferSchema(xr));
                }
            } catch (Exception ex) {
                MyLogger.log(MethodBase.GetCurrentMethod(), ex);
            } finally { }
        }
        static void handleSchemaInference(XmlSchemaSet v) {
            CodeCompileUnit ccu;
            CodeNamespace ns;
            CodeTypeDeclaration ctd;
            string key;

            ccu = new CodeCompileUnit();
            ccu.Namespaces.Add(ns = new CodeNamespace());
            ns.Imports.AddRange(new CodeNamespaceImport[] {
                //new CodeNamespaceImport("System.Xml.Schema"),
                new CodeNamespaceImport("System.Xml.Serialization")
            });
            foreach (XmlSchema xs in v.Schemas()) {
                ns.Types.Clear();
                foreach (XmlSchemaElement xse in xs.Items) {
                    if (xse.ElementSchemaType is XmlSchemaComplexType) {
                        ns.Types.Add(ctd = new CodeTypeDeclaration(key = xse.Name));
                        MyLogger.log(MethodBase.GetCurrentMethod(), "Adding type: " + key);
                        populateComplexType(ns, ctd, xse.ElementSchemaType as XmlSchemaComplexType);
                    } else {
                        MyLogger.log(MethodBase.GetCurrentMethod(), "unhandled: " + xse.ElementSchemaType);
                    }
                }
                outputResult(ccu);
            }
        }

        static void outputResult(CodeCompileUnit ccu) {
            CodeDomProvider cdp = new CSharpCodeProvider();
            CodeGeneratorOptions opts = new CodeGeneratorOptions();
            StringBuilder sb;
            //OpenFileDialog ofd;
            SaveFileDialog sfd;
            bool doDefault = true;

            opts.BlankLinesBetweenMembers = false;
            opts.BracingStyle = "BLOCK";
            opts.ElseOnClosing = true;

            if (MessageBox.Show("Generate output-file?", "Action", MessageBoxButtons.OKCancel) == DialogResult.OK) {
                sfd = new SaveFileDialog();
                sfd.Filter = cdp.FileExtension.ToUpper() + " file|*." + cdp.FileExtension;
                sfd.FilterIndex = 0;
                sfd.InitialDirectory = Directory.GetCurrentDirectory();
                if (sfd.ShowDialog() == DialogResult.OK) {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName)) {
                        cdp.GenerateCodeFromCompileUnit(ccu, sw, opts);
                    }
                    doDefault = false;
                }
            }
            if (doDefault) {
                using (StringWriter sw = new StringWriter(sb = new StringBuilder())) {
                    cdp.GenerateCodeFromCompileUnit(ccu, sw, opts);
                }
                Trace.WriteLine(sb.ToString());
            }
        }

        static void populateComplexType(CodeNamespace ns, CodeTypeDeclaration ctd, XmlSchemaComplexType xsct) {
            XmlSchemaSequence xss;
            XmlSchemaElement e;
            CodeMemberField f;
            CodeTypeDeclaration ctdNew = null;
            XmlSchemaSimpleType xsst;
            string key, fname;

            if (xsct.ContentTypeParticle != null) {
                if (xsct.ContentTypeParticle is XmlSchemaSequence) {
                    xss = xsct.ContentTypeParticle as XmlSchemaSequence;
                    foreach (var avar in xss.Items) {
                        if (avar is XmlSchemaElement) {
                            e = avar as XmlSchemaElement;
                            if (e.ElementSchemaType is XmlSchemaComplexType) {
                                ctdNew = readComplexType(ns, e.Name, e.ElementSchemaType as XmlSchemaComplexType);
                                fname = e.QualifiedName.Name;
                                ctd.Members.Add(
                                  f = new CodeMemberField(ctdNew.Name, "_" + char.ToLower(fname[0]) + fname.Substring(1)));
                                f.Attributes = MemberAttributes.Public;
                                f.CustomAttributes.Add(createAttribute(ctrSerialAttr, fname));
                            } else if (e.ElementSchemaType is XmlSchemaSimpleType) {
                                xsst = e.ElementSchemaType as XmlSchemaSimpleType;
                                ctdNew = ctd;
                                fname = e.QualifiedName.Name;
                                CodeTypeReference ctr = new CodeTypeReference(typeof(object));
                                switch (key = xsst.QualifiedName.Name) {
                                    case "boolean": ctr = new CodeTypeReference(typeof(bool)); break;
                                    case "string": ctr = new CodeTypeReference(typeof(string)); break;
                                    default:
                                        MyLogger.log(MethodBase.GetCurrentMethod(), "Unhandled: " + key);
                                        break;
                                }
                                ctd.Members.Add(f = new CodeMemberField(ctr, "_" + char.ToLower(fname[0]) + fname.Substring(1)));
                                f.Attributes = MemberAttributes.Public;
                                f.CustomAttributes.Add(createAttribute(ctrSerialAttr, fname));
                            } else
                                throw new InvalidOperationException("unhandled type:" + e.ElementSchemaType);
                        } else
                            MyLogger.log(MethodBase.GetCurrentMethod(), "unhandled: " + avar);
                    }
                } else {
                    MyLogger.log(MethodBase.GetCurrentMethod(), "not sequence");
                }
            }
        }

        static CodeAttributeDeclaration createAttribute(CodeTypeReference ctr, string fname) {
            return new CodeAttributeDeclaration(ctr, new CodeAttributeArgument(new CodePrimitiveExpression(fname)));
        }

        static CodeAttributeDeclaration createAttribute(CodeTypeReference ctr, CodeAttributeArgument cad) {
            return new CodeAttributeDeclaration(ctr, cad);
        }

        static CodeAttributeDeclaration createAttribute(string attrType, string fname) {
            return createAttribute(new CodeTypeReference(attrType),
                new CodeAttributeArgument(new CodePrimitiveExpression(fname)));
        }

        static CodeAttributeDeclaration createAttribute(Type type, string fname) {
            return createAttribute(type.Name, fname);
        }

        static CodeTypeDeclaration readComplexType(CodeNamespace ns, string className, XmlSchemaComplexType xcdt) {
            CodeTypeDeclaration ret = null;

            if (ns.Types.Count > 0)
                foreach (CodeTypeDeclaration ctd in ns.Types)
                    if (string.Compare(ctd.Name, className, true) == 0) {
                        ret = ctd;
                        break;
                    }
            if (ret == null) {
                MyLogger.log(MethodBase.GetCurrentMethod(), "Adding type: " + className);
                ns.Types.Add(ret = new CodeTypeDeclaration(className));
                ret.IsPartial = true;
            }
            populateComplexType(ns, ret, xcdt);
            return ret;
        }
    }
}