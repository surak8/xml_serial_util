using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CSharp;

namespace NSLaserCfg {
    class SomeClass {
        #region fields
        CodeCompileUnit ccu;
        CodeNamespace ns;
        bool issueFound = false;
        #endregion
        void Xrs_ValidationEventHandler(object sender, System.Xml.Schema.ValidationEventArgs e) {
            MyLogger.log(MethodBase.GetCurrentMethod());
        }

        void doDeserialization(string filename) {
            XmlDeserializationEvents xde;
            XmlSerializer xs;
            XmlReaderSettings xrs;
            StringBuilder sb;
            object anObj;

            ccu = new CodeCompileUnit();
            ccu.Namespaces.Add(ns = new CodeNamespace());
            ns.Imports.AddRange(
                new CodeNamespaceImport[] {
                    new CodeNamespaceImport ("System.Xml"),
                    new CodeNamespaceImport ("System.Xml.Serialization"),
                });
            try {
                xs = new XmlSerializer(typeof(MarkingTaskGroup[]));
                xrs = new XmlReaderSettings();
                xrs.ValidationEventHandler += Xrs_ValidationEventHandler;
                xrs.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.AllowXmlAttributes | System.Xml.Schema.XmlSchemaValidationFlags.ReportValidationWarnings;
                using (XmlReader xr = XmlReader.Create(filename, xrs)) {
                    xde = new XmlDeserializationEvents();
                    xde.OnUnknownAttribute = this.unknownAttribute;
                    xde.OnUnknownElement = this.unknownElement;
                    xde.OnUnknownNode = this.unknownNode;
                    xde.OnUnreferencedObject = this.unreferencedObject;
                    anObj = xs.Deserialize(xr, xde);
                    MyLogger.log(MethodBase.GetCurrentMethod());
                }
            } catch (Exception ex) {
                MyLogger.log(MethodBase.GetCurrentMethod(), ex);
            } finally {
                if (issueFound) {
                    showResult(sb = new StringBuilder());
                }
            }
        }

        void showResult(StringBuilder sb) {
            //throw new NotImplementedException();
            CodeDomProvider cdp = new CSharpCodeProvider();
            CodeGeneratorOptions opts = new CodeGeneratorOptions();
            opts.BlankLinesBetweenMembers = false;
            opts.ElseOnClosing = true;

            using (StringWriter xw = new StringWriter(sb = new StringBuilder())) {
                cdp.GenerateCodeFromCompileUnit(ccu, xw, opts);
            }
            Trace.WriteLine(sb.ToString());
        }
        void addToNamespace(string nameSpace, string className, string fieldName) {
            CodeNamespace nsThis = null;


            nsThis = null;
            foreach (CodeNamespace anns in ccu.Namespaces) {
                if (string.Compare(anns.Name, nameSpace) == 0) {
                    nsThis = anns;
                    break;
                }
            }
            if (nsThis == null) {
                ccu.Namespaces.Add(nsThis = new CodeNamespace(nameSpace));

            }
            addTypeInfo(nsThis, className, fieldName);
        }
        void addTypeInfo(CodeNamespace nsThis, string className, string fieldName) {
            CodeTypeDeclaration ctd = null;
            foreach (CodeTypeDeclaration atype in nsThis.Types)
                if (string.Compare(atype.Name, className, true) == 0) {
                    ctd = atype;
                    break;
                }
            if (ctd == null) {
                nsThis.Types.Add(ctd = new CodeTypeDeclaration(className));
                ctd.IsPartial = true;
                //ctd.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(XmlRootAttribute))));
                ctd.CustomAttributes.Add(new CodeAttributeDeclaration("XmlRoot"));
            }
            addField(ctd, fieldName);
        }


        void addField(CodeTypeDeclaration ctd, string fieldName) {
            CodeMemberField f = null;
            string fname;

            fname = "_" + Char.ToLower(fieldName[0]) + fieldName.Substring(1);
            foreach (CodeTypeMember ctm in ctd.Members) {
                if (ctm is CodeMemberField) {
                    if (string.Compare(ctm.Name, fname, true) == 0) {
                        f = ctm as CodeMemberField;
                        f.Attributes = MemberAttributes.Public;
                    }
                }
            }
            if (f == null) {
                ctd.Members.Add(f = new CodeMemberField(typeof(string), fname));
                Trace.WriteLine("adding field: " + fname);
                f.Attributes = MemberAttributes.Public;
                f.CustomAttributes.Add(
                    new CodeAttributeDeclaration(
                        //new CodeTypeReference(typeof(XmlElementAttribute)),
                        "XmlElement",
                                               new CodeAttributeArgument(new CodePrimitiveExpression(fieldName))));
            }
        }


        void unreferencedObject(object sender, UnreferencedObjectEventArgs e) {
            MyLogger.log(MethodBase.GetCurrentMethod());
        }

        void unknownNode(object sender, XmlNodeEventArgs e) {
            int n;
            string[] parts;
            string className, nameSpace;
            //CodeNamespace nsThis;
            //CodeTypeDeclaration ctd;

            if (e.ObjectBeingDeserialized != null) {

                if (!issueFound)
                    issueFound = true;
                parts = e.ObjectBeingDeserialized.GetType().FullName.Split('.');
                //if ((pos=))

                //Trace.WriteLine("here");
                if ((n = parts.Length) < 2) {
                    nameSpace = string.Empty;
                    className = parts[0];
                } else {
                    nameSpace = string.Join(".", parts, 0, n - 1);
                    className = parts[n - 1];
                }
                addToNamespace(nameSpace, className, e.Name);

            } else
                MyLogger.log(MethodBase.GetCurrentMethod(), "Found " + e.NodeType + " '" + e.Name + "' on " + e.ObjectBeingDeserialized.GetType().FullName);
        }

        void unknownElement(object sender, XmlElementEventArgs e) {
            MyLogger.log(MethodBase.GetCurrentMethod(), e.ObjectBeingDeserialized.GetType() + ": found " + e.Element.Name);
        }

        void unknownAttribute(object sender, XmlAttributeEventArgs e) {
            MyLogger.log(MethodBase.GetCurrentMethod());
        }

    }
}