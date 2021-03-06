﻿/***************************************
//   Copyright 2014 - Svetoslav Vasilev

//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
*****************************************/

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;

using Microsoft.XLANGs.BaseTypes;

namespace TransMock.TestUtils.BizTalk
{
    /// <summary>
    /// This class contains helper methods for TransMock functionality to be invoked from within BizTalk server orchestrations
    /// </summary>
    public static class MockHelper
    {
        /// <summary>
        /// Mocks a dynamic send port with the given name by setting the necessary transport properties on the outbound message
        /// only in the case a TransMock test case is being executed
        /// </summary>
        /// <param name="portName">The name of the send port as defined in the orchestration</param>
        /// <param name="outboundMessage">The outbound message instance that is to be sent over the dynamic send port</param>
        /// <returns>An instance of the MockTransportConfig class if the orchestration is executed within the context of a TransMock test case. Otherwise a null is returned.</returns>
        public static MockTransportConfig MockDynamicSendPort(
            string portName,
            XLANGMessage outboundMessage)
        {
            System.Diagnostics.Debug.WriteLine("MockDynamicSendPort(portName, originalAdapter, XLANGMessage) called.");

            return MockDynamicSendPort(portName, null, outboundMessage);
        }

        /// <summary>
        /// Mocks a dynamic send port with the given name by setting the necessary transport properties with the specified custom behaviors
        /// on the outbound message only in the case a TransMock test case is being executed
        /// </summary>
        /// <param name="portName">The name of the send port as defined in the orchestration</param>                
        /// <param name="customBehaviorConfig">The custom behavior configuration to be applied</param>
        /// <param name="outboundMessage">The outbound message instance that is to be sent over the dynamic send port</param>
        /// <returns>An instance of the <see cref="MockTransportConfig"/> class which contains the configuration for the mock adapter transport for the dynamic send port</returns>
        public static MockTransportConfig MockDynamicSendPort(
            string portName,             
            string customBehaviorConfig, 
            XLANGMessage outboundMessage)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MockDynamicSendPort(portName, originalAdapter, customBehaviorConfig, XLANGMessage) called.");

                bool applyMock = IsTransMockTestCaseExecuting();

                if (applyMock)
                {
                    System.Diagnostics.Debug.WriteLine("Applying mock transport settings");

                    // Clear any previously set transport specific properties
                    ClearOriginalWCFAdapterProperties(outboundMessage);

                    // Then apply the mock transport config properties to the message
                    ApplyMockTransportConfig(outboundMessage, customBehaviorConfig);

                    var mockConfig = new MockTransportConfig(portName);

                    return mockConfig;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Not executing in TransMock test case. Returning null.");

                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "MockDynamicSendPort(portName, customBehaviorConfig, XLANGMessage) threw an exception: "
                    + ex.Message);

                return null;
            }
        }

        /// <summary>
        /// Checks whether a TransMock test case is currently under execution
        /// </summary>
        /// <returns>True if the TransMock beacon is on, otherwise false</returns>
        private static bool IsTransMockTestCaseExecuting()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("IsTransMockTestCaseExecuting() called");

                using (NamedPipeClientStream beaconClient = 
                    new NamedPipeClientStream(
                        "localhost", 
                        "TransMockBeacon",
                        PipeDirection.InOut, 
                        PipeOptions.Asynchronous))
                {
                    System.Diagnostics.Debug.WriteLine("Connecting to the beacon");

                    beaconClient.Connect(10);
                    
                    // Closing the stream immediately after connecting
                    System.Diagnostics.Debug.WriteLine("Disonnecting from the beacon");                    
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IstransMockTestCaseExecuting() threw exception: " + ex.Message);

                return false;
            }
        }       

        /// <summary>
        /// Applies the TransMock transport configuration to the context of the provided message 
        /// </summary>
        /// <param name="outboundMessage">The outbound message instance that is to be sent over the dynamic send port</param>
        /// <param name="customBehaviorConfig">A string representing the custom behavior configuration</param>
        private static void ApplyMockTransportConfig(XLANGMessage outboundMessage, string customBehaviorConfig)
        {
            // Adding the mock binding properties to tme message context                    
            outboundMessage.SetPropertyValue(typeof(WCF.BindingType), "mockBinding");
            outboundMessage.SetPropertyValue(
                typeof(WCF.BindingConfiguration),
                @"<binding name=""mockBinding"" Encoding=""UTF-8"" />");
            outboundMessage.SetPropertyValue(typeof(WCF.Action), "*");

            if (string.IsNullOrEmpty(customBehaviorConfig))
            {
                outboundMessage.SetPropertyValue(
                    typeof(WCF.EndpointBehaviorConfiguration),
                    @"<behavior name=""EndpointBehavior"" />");
            }
            else
            {
                outboundMessage.SetPropertyValue(
                    typeof(WCF.EndpointBehaviorConfiguration),
                    customBehaviorConfig);
            }

            outboundMessage.SetPropertyValue(typeof(WCF.OutboundBodyLocation), "UseTemplate");
            outboundMessage.SetPropertyValue(
                typeof(WCF.OutboundXmlTemplate),
                @"<bts-msg-body xmlns=""http://www.microsoft.com/schemas/bts2007"" encoding=""base64""/>");

            outboundMessage.SetPropertyValue(typeof(WCF.InboundBodyLocation), @"UseBodyPath");
            outboundMessage.SetPropertyValue(typeof(WCF.InboundBodyPathExpression), @"/MessageContent");
            outboundMessage.SetPropertyValue(typeof(WCF.InboundNodeEncoding), @"Base64");
            outboundMessage.SetPropertyValue(typeof(WCF.PropagateFaultMessage), true);

            outboundMessage.SetPropertyValue(typeof(WCF.UseSSO), false);
            outboundMessage.SetPropertyValue(typeof(WCF.EnableTransaction), false);
        }

        /// <summary>
        /// Clears the properties set for the original WCF transport
        /// </summary>
        /// <param name="outboundMessage">The message which will have its context cleared for the original adapter properties</param>        
        private static void ClearOriginalWCFAdapterProperties(XLANGMessage outboundMessage)
        {
            System.Diagnostics.Debug.WriteLine("ClearOriginalTransportProperties(outboundMessage) called.");

            try
            {
                if (outboundMessage.GetPropertyValue(typeof(WCF.AffiliateApplicationName)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.AffiliateApplicationName), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ClientCertificate)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ClientCertificate), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.Identity)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.Identity), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.IsolationLevel)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.IsolationLevel), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.MessageClientCredentialType)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.MessageClientCredentialType), "None");
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ProxyAddress)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ProxyAddress), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ProxyUserName)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ProxyUserName), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ProxyPassword)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ProxyPassword), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ProxyToUse)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ProxyToUse), "None");
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ReplyToAddress)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ReplyToAddress), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.ReferencedBindings)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.ReferencedBindings), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.To)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.To), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.SecurityMode)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.SecurityMode), "None");
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.TransportProtectionLevel)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.TransportProtectionLevel), "None");
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.UserName)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.UserName), string.Empty);
                }

                if (outboundMessage.GetPropertyValue(typeof(WCF.Password)) != null)
                {
                    outboundMessage.SetPropertyValue(typeof(WCF.Password), string.Empty);
                }                
            }
            catch (Exception ex)
            {                
                System.Diagnostics.Debug.WriteLine(
                    "ClearOriginalTransportProperties() failed with exception: "
                    + ex.Message);
            }
        }        
    }
}
