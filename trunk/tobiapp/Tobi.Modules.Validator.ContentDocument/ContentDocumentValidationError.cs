﻿using Tobi.Common.Validation;
using urakawa.core;

namespace Tobi.Plugin.Validator.ContentDocument
{
    public enum ContentDocumentErrorType
    {
        UndefinedElement,
        InvalidElementSequence,
        MissingDtd
    }
    
    //expectation of error types and available data
    //UndefinedElement => supply Target
    //InvalidElementSequence => supply Target, AllowedChildNodes, optionally BeginningOfError
    //MissingDtd => supply DtdIdentifier
    public class ContentDocumentValidationError : ValidationItem
    {
        public ContentDocumentErrorType ErrorType { get; set; }
        public string AllowedChildNodes { get; set; }
        public TreeNode Target { get; set;}
        public TreeNode BeginningOfError { get; set; }
        public string DtdIdentifier { get; set; }

        //todo: is there a problem overriding the base property Message {get; set;} with this new Message {get;} (no set method) ?
        public new string Message
        {
            get
            {
                string targetNodeName = "";
                string problemChildNodeName = "";
                string targetText = "";
                string problemChildText = "";

                if (Target != null)
                {
                    targetText = Target.GetTextMediaFlattened();
                    if (Target.GetXmlElementQName() != null)
                        targetNodeName = Target.GetXmlElementQName().LocalName;
                }
                if (BeginningOfError != null && BeginningOfError.GetXmlElementQName() != null)
                    problemChildNodeName = BeginningOfError.GetXmlElementQName().LocalName;

                if (ErrorType == ContentDocumentErrorType.InvalidElementSequence)
                {
                    return string.Format("Element {0} contains an invalid sequence of child elements", targetNodeName);
                }
                if (ErrorType == ContentDocumentErrorType.MissingDtd)
                {
                    return string.Format("No DTD found for {0}", DtdIdentifier);
                }
                if (ErrorType == ContentDocumentErrorType.UndefinedElement)
                {
                   return string.Format("No element definition found for {0}", targetNodeName);
                }
                return "Unspecified error";
            }
        }
        public ContentDocumentValidationError()
        {
            Severity = ValidationSeverity.Error;
        }
    }
}