using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Commercc.Transaction.Common;
using A = AuthorizeNet;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers.Bases;

public class Process
{
    public stcTransactionDetailSet ProcessTransaction(stcTransactionDetailSet sT)
    {
        A.Api.Contracts.V1.createTransactionResponse iR = null;
        A.Api.Contracts.V1.ARBCreateSubscriptionResponse iRCSR = null;
        A.Api.Contracts.V1.ARBUpdateSubscriptionResponse iRUSR = null;
        A.Api.Contracts.V1.ARBCancelSubscriptionResponse iRCRSR = null;
        A.Api.Contracts.V1.getTransactionListResponse iRGTLR = null;

        try
        {
            switch (sT.transaction.paymentStatusId)
            {
                case enTransactionStatusList.Authorized:
                    {
                        iR = AuthCaptureTransaction(sT);
                        break;
                    }

                case enTransactionStatusList.AuthCaptured:
                    {
                        iR = AuthCaptureTransaction(sT);
                        break;
                    }

                case enTransactionStatusList.Captured:
                    {
                        iR = CaptureTransaction(sT);
                        break;
                    }

                case enTransactionStatusList.Voided:
                    {
                        iR = VoidTransaction(sT);
                        break;
                    }
                case enTransactionStatusList.Refunded:
                    {
                        iR = RefundTransaction(sT);
                        break;
                    }
                case enTransactionStatusList.CreateSubscription:
                    {
                        iRCSR = CreateSubscription(sT);
                        break;
                    }
                case enTransactionStatusList.UpdateSubscription:
                    {
                        iRUSR = UpdateSubscription(sT);
                        break;
                    }
                case enTransactionStatusList.CancelSubscription:
                    {
                        iRCRSR = CancelSubscription(sT);
                        break;
                    }
                case enTransactionStatusList.GetTransaction:
                    {
                        iRGTLR = GetTransactionList(sT);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            // This is the ONLY Point where it is indicated that RESPONSE has ERROR

            sT.transaction.response.responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.Transaction_Process_General_Error };
        }

        // Convert AuthorizeNet Response to Standard Response Structure
        switch (sT.transaction.paymentStatusId)
        {
            case enTransactionStatusList.Authorized:
            case enTransactionStatusList.AuthCaptured:
            case enTransactionStatusList.Captured:
            case enTransactionStatusList.Voided:
            case enTransactionStatusList.Refunded:
                {
                    stcResponseSet sR = GetTransactionResponse(ref sT, iR);

                    // Attach Response back to Transaction
                    sT.transaction.response = sR;
                    break;
                }

            case enTransactionStatusList.CreateSubscription:
                {
                    stcResponseSet sR = GetCreateSubscriptionResponse(ref sT, iRCSR);
                    sT.transaction.response = sR;
                    break;
                }

            case enTransactionStatusList.UpdateSubscription:
                {
                    stcResponseSet sR = GetUpdateSubscriptionResponse(ref sT, iRUSR);
                    sT.transaction.response = sR;
                    break;
                }
            case enTransactionStatusList.CancelSubscription:
                {
                    stcResponseSet sR = GetCancelSubscriptionResponse(ref sT, iRCRSR);
                    sT.transaction.response = sR;
                    break;
                }
            case enTransactionStatusList.GetTransaction:
                {
                    stcResponseSet sR = GetTransactionListResponse(ref sT, iRGTLR);
                    sT.transaction.response = sR;
                    break;
                }
        }

        return sT;
    }

    private A.Api.Contracts.V1.createTransactionResponse AuthCaptureTransaction(stcTransactionDetailSet sT)
    {
        sT.customer.payment.card.cardExpiration = string.Format("{0:00}{1}", sT.customer.payment.card.cardExpirationMonthId, sT.customer.payment.card.cardExpirationYearId.ToString().Substring(2));

        var customerProfile = new customerAddressType
        {
            company = sT.customer.billing.companyName,
            firstName = sT.customer.billing.firstName,
            lastName = sT.customer.billing.lastName,
            address = sT.customer.billing.streetI,
            city = sT.customer.billing.city,
            state = sT.customer.billing.state,
            zip = sT.customer.billing.zipcode,
            country = sT.customer.billing.country.country,
            email = sT.customer.billing.email,
            phoneNumber = sT.customer.billing.phone,
            faxNumber = sT.customer.billing.fax
        };

        List<customerAddressType> addressInfoList = new List<customerAddressType>();
        customerAddressType shipAddress = new customerAddressType();
        shipAddress.company = sT.customer.shipping.companyName;
        shipAddress.firstName = sT.customer.shipping.firstName;
        shipAddress.lastName = sT.customer.shipping.lastName;
        shipAddress.address = sT.customer.shipping.streetI;
        shipAddress.city = sT.customer.shipping.city;
        shipAddress.state = sT.customer.shipping.state;
        shipAddress.zip = sT.customer.shipping.zipcode;
        shipAddress.country = sT.customer.shipping.country.country;

        addressInfoList.Add(shipAddress);

        var customerShippingAddress = new customerProfileType
        {
            shipToList = addressInfoList.ToArray()
        };

        var creditCard = new object();

        if (sT.customer.payment.paymentModeId == enPaymentModeList.CreditCard | sT.customer.payment.paymentModeId == enPaymentModeList.DebitCard | sT.customer.payment.paymentModeId == enPaymentModeList.GiftCard)
        {
            creditCard = new creditCardType
            {
                cardNumber = sT.customer.payment.card.cardNumber,
                expirationDate = sT.customer.payment.card.cardExpiration,
                cardCode = sT.customer.payment.card.cardCVV
            };
        }
        else if (sT.customer.payment.paymentModeId == enPaymentModeList.ECheck)
        {
            var bankDetails = new bankAccountType
            {
                bankName = sT.customer.payment.eCheck.bankName,
                routingNumber = sT.customer.payment.eCheck.bankABA,
                accountType = (A.Api.Contracts.V1.bankAccountTypeEnum)Enum.Parse(typeof(A.Api.Contracts.V1.bankAccountTypeEnum), sT.customer.payment.eCheck.bankAccountType),
                nameOnAccount = sT.customer.payment.eCheck.bankAccountName,
                accountNumber = sT.customer.payment.eCheck.bankAccountNumber,
                checkNumber = sT.customer.payment.eCheck.bankCheckNumber
            };
        }

        //standard api call to retrieve response
        var paymentType = new paymentType { Item = creditCard };

        var transactionRequest = new transactionRequestType
        {
            transactionType = transactionTypeEnum.authOnlyTransaction.ToString(),
            amount = sT.cart.cartAmount,
            payment = paymentType,
            customerIP = sT.customer.ipAddress,
            billTo = customerProfile,
            shipTo = shipAddress,
            order = new orderType
            {
                invoiceNumber = sT.cart.invoiceNumber,
                description = sT.cart.cartDescription
            }
        };

        OpenGateway(sT.merchant.processor);

        var request = new createTransactionRequest { transactionRequest = transactionRequest };

        // instantiate the controller that will call the service
        var controller = new createTransactionController(request);
        controller.Execute();

        // get the response from the service (errors contained if any)
        var response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.createTransactionResponse CaptureTransaction(stcTransactionDetailSet sT)
    {
        sT.customer.payment.card.cardExpiration = string.Format("{0:00}{1}", sT.customer.payment.card.cardExpirationMonthId, sT.customer.payment.card.cardExpirationYearId.ToString().Substring(2));
        string strCardNumber = sT.customer.payment.card.cardNumber.Remove(0, sT.customer.payment.card.cardNumber.Length - 4);

        OpenGateway(sT.merchant.processor);

        var creditCard = new creditCardType
        {
            cardNumber = sT.customer.payment.card.cardNumber,
            expirationDate = sT.customer.payment.card.cardExpiration,
        };

        var paymentType = new paymentType { Item = creditCard };

        var transactionRequest = new transactionRequestType
        {
            // capture the funds that authorized through another channel
            transactionType = transactionTypeEnum.captureOnlyTransaction.ToString(),
            // Change the amount that needs to be captured as required
            amount = sT.cart.cartAmount,
            payment = paymentType,
            // Change the authCode that came from successfully authorized transaction through any channel.
            authCode = sT.transaction.authorizationCode
        };

        var request = new createTransactionRequest
        {
            transactionRequest = transactionRequest
        };

        // instantiate the controller that will call the service
        var controller = new createTransactionController(request);
        controller.Execute();

        // get the response from the service (errors contained if any)
        var response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.createTransactionResponse VoidTransaction(stcTransactionDetailSet sT)
    {
        OpenGateway(sT.merchant.processor);

        var transactionRequest = new transactionRequestType
        {
            transactionType = transactionTypeEnum.voidTransaction.ToString(),
            refTransId = sT.transaction.transactionId.ToString()
        };

        var request = new createTransactionRequest { transactionRequest = transactionRequest };

        // instantiate the controller that will call the service
        var controller = new createTransactionController(request);
        controller.Execute();

        // get the response from the service (errors contained if any)
        var response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.createTransactionResponse RefundTransaction(stcTransactionDetailSet sT)
    {
        sT.customer.payment.card.cardExpiration = string.Format("{0:00}{1}", sT.customer.payment.card.cardExpirationMonthId, sT.customer.payment.card.cardExpirationYearId.ToString().Substring(2));
        string strCardNumber = sT.customer.payment.card.cardNumber.Remove(0, sT.customer.payment.card.cardNumber.Length - 4);
        OpenGateway(sT.merchant.processor);

        var creditCard = new creditCardType
        {
            cardNumber = sT.customer.payment.card.cardNumber,
            expirationDate = sT.customer.payment.card.cardExpiration,
        };

        //standard api call to retrieve response
        var paymentType = new paymentType { Item = creditCard };

        var transactionRequest = new transactionRequestType
        {
            transactionType = transactionTypeEnum.refundTransaction.ToString(),
            payment = paymentType,
            amount = sT.cart.cartAmount,
            refTransId = sT.transaction.transactionId.ToString()
        };

        var request = new createTransactionRequest { transactionRequest = transactionRequest };

        // instantiate the controller that will call the service
        var controller = new createTransactionController(request);
        controller.Execute();

        // get the response from the service (errors contained if any)
        var response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.ARBCreateSubscriptionResponse CreateSubscription(stcTransactionDetailSet sT)
    {
        OpenGateway(sT.merchant.processor);

        paymentScheduleTypeInterval interval = new paymentScheduleTypeInterval();

        // months can be indicated between 1 and 12
        interval.length = sT.subscription.intervalLength;
        interval.unit = ARBSubscriptionUnitEnum.days;

        paymentScheduleType schedule = new paymentScheduleType
        {
            interval = interval,
            startDate = DateTime.Now.AddDays(sT.subscription.noOfAddDays),  // start date should be tomorrow
            totalOccurrences = sT.subscription.totalOccurrences, // 999 indicates no end date
            trialOccurrences = sT.subscription.trialOccurrences
        };

        var creditCard = new creditCardType
        {
            cardNumber = sT.customer.payment.card.cardNumber,
            expirationDate = sT.customer.payment.card.cardExpiration,
        };

        //standard api call to retrieve response
        paymentType cc = new paymentType { Item = creditCard };

        nameAndAddressType addressInfo = new nameAndAddressType()
        {
            firstName = sT.customer.billing.firstName,
            lastName = sT.customer.billing.lastName
        };

        ARBSubscriptionType subscriptionType = new ARBSubscriptionType()
        {
            amount = sT.cart.cartAmount,
            trialAmount = sT.subscription.trialAmount,
            paymentSchedule = schedule,
            billTo = addressInfo,
            payment = cc
        };

        var request = new ARBCreateSubscriptionRequest { subscription = subscriptionType };

        var controller = new ARBCreateSubscriptionController(request);          // instantiate the controller that will call the service
        controller.Execute();

        ARBCreateSubscriptionResponse response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.ARBUpdateSubscriptionResponse UpdateSubscription(stcTransactionDetailSet sT)
    {
        OpenGateway(sT.merchant.processor);

        paymentScheduleType schedule = new paymentScheduleType
        {
            startDate = DateTime.Now.AddDays(sT.subscription.noOfAddDays),      // start date should be tomorrow
            totalOccurrences = sT.subscription.totalOccurrences   // 999 indicates no end date
        };

        #region Payment Information
        var creditCard = new creditCardType
        {
            cardNumber = sT.customer.payment.card.cardNumber,
            expirationDate = sT.customer.payment.card.cardExpiration,
        };

        //standard api call to retrieve response
        paymentType cc = new paymentType { Item = creditCard };
        #endregion

        nameAndAddressType addressInfo = new nameAndAddressType()
        {
            firstName = sT.customer.billing.firstName,
            lastName = sT.customer.billing.lastName
        };

        ARBSubscriptionType subscriptionType = new ARBSubscriptionType()
        {
            amount = sT.cart.cartAmount,
            paymentSchedule = schedule,
            billTo = addressInfo,
            payment = cc,
        };

        //Please change the subscriptionId according to your request
        var request = new ARBUpdateSubscriptionRequest { subscription = subscriptionType, subscriptionId = sT.subscription.subscriptionId };
        var controller = new ARBUpdateSubscriptionController(request);
        controller.Execute();

        ARBUpdateSubscriptionResponse response = controller.GetApiResponse();

        return response;
    }

    private A.Api.Contracts.V1.ARBCancelSubscriptionResponse CancelSubscription(stcTransactionDetailSet sT)
    {
        OpenGateway(sT.merchant.processor);

        //Please change the subscriptionId according to your request
        var request = new ARBCancelSubscriptionRequest { subscriptionId = sT.subscription.subscriptionId };
        var controller = new ARBCancelSubscriptionController(request);                          // instantiate the controller that will call the service
        controller.Execute();

        ARBCancelSubscriptionResponse response = controller.GetApiResponse();
        return response;
    }

    private A.Api.Contracts.V1.getTransactionListResponse GetTransactionList(stcTransactionDetailSet sT)
    {
        OpenGateway(sT.merchant.processor);

        // unique batch id
        string batchId = sT.subscription.batchId;

        var request = new getTransactionListRequest();
        request.batchId = batchId;
        request.paging = new Paging
        {
            limit = 10,
            offset = 1
        };
        request.sorting = new TransactionListSorting
        {
            orderBy = TransactionListOrderFieldEnum.id,
            orderDescending = true
        };

        // instantiate the controller that will call the service
        var controller = new getTransactionListController(request);
        controller.Execute();

        // get the response from the service (errors contained if any)
        var response = controller.GetApiResponse();

        return response;
    }

    private void OpenGateway(stcProcessorSet pP)
    {
        ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = A.Environment.SANDBOX;

        ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType()
        {
            name = pP.gateway.apiLoginId,
            ItemElementName = ItemChoiceType.transactionKey,
            Item = pP.gateway.apiSecret,
        };
    }
    protected internal stcResponseSet GetTransactionResponse(ref stcTransactionDetailSet sT, A.Api.Contracts.V1.createTransactionResponse iR)
    {
        stcResponseSet sR = sT.transaction.response;
        stcResultSet stcRErrors = new stcResultSet();
        stcResultSet stcR = new stcResultSet();

        if (sR.responseErrorCode.errorMessage == null)
            sR = new stcResponseSet()
            {
                responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.No_Error, errorMessage = "" }
            };

        if (iR.messages.resultCode == messageTypeEnum.Ok)
        {
            if (iR.transactionResponse.messages != null)
            {
                if (iR.transactionResponse.responseCode == "1")
                    sR.responseStatusCode = enResponseCodeList.Approved;
                else if (iR.transactionResponse.responseCode == "2")
                    sR.responseStatusCode = enResponseCodeList.Declined;
                else if (iR.transactionResponse.responseCode == "3")
                    sR.responseStatusCode = enResponseCodeList.Error;
                else if (iR.transactionResponse.responseCode == "4")
                    sR.responseStatusCode = enResponseCodeList.Under_Review;

                sR.referenceId = iR.refId;
                sR.responseCode = (enResponseCodeList)Enum.Parse(typeof(enResponseCodeList), iR.transactionResponse.responseCode);
                sR.authCode = iR.transactionResponse.authCode;

                int count = 0;

                foreach (enAVSResultCodeList AVSitemList in Enum.GetValues(typeof(enAVSResultCodeList)))
                {
                    if (AVSitemList.ToString().Substring(0, 1) == iR.transactionResponse.avsResultCode)
                    {
                        sR.avsResultCode = ((Commercc.Transaction.Common.enAVSResultCodeList[])Enum.GetValues(typeof(enAVSResultCodeList)))[count];
                        count = 0;
                        break;
                    }
                    count = count + 1;
                }

                foreach (enCVVResultCodeList CVVitemList in Enum.GetValues(typeof(enCVVResultCodeList)))
                {
                    if (CVVitemList.ToString().Substring(0, 1) == iR.transactionResponse.cvvResultCode)
                    {
                        sR.cvvResultCode = ((Commercc.Transaction.Common.enCVVResultCodeList[])Enum.GetValues(typeof(enCVVResultCodeList)))[count];
                        count = 0;
                        break;
                    }
                    count = count + 1;
                }

                sR.cavvResultCode = (enCAVVResultCodeList)Enum.Parse(typeof(enCAVVResultCodeList), iR.transactionResponse.cavvResultCode);
                sR.rawResponseCode = iR.transactionResponse.rawResponseCode;
                sR.transactionNumber = iR.transactionResponse.transId;
                sR.refTransactionNumber = iR.transactionResponse.refTransID;
                sR.transactionHash = iR.transactionResponse.transHash;
                sR.testTransaction = iR.transactionResponse.testRequest;
                sR.accountNumber = iR.transactionResponse.accountNumber;
                sR.accountType = (enAccountTypeList)Enum.Parse(typeof(enAccountTypeList), iR.transactionResponse.accountType);
                sR.transactionHash = iR.transactionResponse.transHashSha2;
                //sR.SupplementalDataIndicator = iR.transactionResponse.transHashSha2
                //sR.networkTransactionId = iR.transactionResponse.transHashSha2;

                if (iR.transactionResponse.shipTo != null)
                {
                    sR.shipping = new stcAddressSet();
                    sR.shipping.country = new stcCountrySet();

                    sR.shipping.companyName = iR.transactionResponse.shipTo.company;
                    sR.shipping.firstName = iR.transactionResponse.shipTo.firstName;
                    sR.shipping.lastName = iR.transactionResponse.shipTo.lastName;

                    sR.shipping.streetI = iR.transactionResponse.shipTo.address;
                    sR.shipping.city = iR.transactionResponse.shipTo.city;
                    sR.shipping.state = iR.transactionResponse.shipTo.state;
                    sR.shipping.country.country = iR.transactionResponse.shipTo.country;
                    sR.shipping.zipcode = iR.transactionResponse.shipTo.zip;
                }

                if (iR.transactionResponse.userFields != null)
                {
                    stcKeyPairSet stcKeyPair = new stcKeyPairSet();
                    stcKeyPair.key = iR.transactionResponse.userFields[0].name;
                    stcKeyPair.value = iR.transactionResponse.userFields[0].value;
                }

                if (iR.transactionResponse.secureAcceptance != null)
                {
                    sR.secureAcceptanceUrl = iR.transactionResponse.secureAcceptance.SecureAcceptanceUrl;
                }

                stcR.resultCode = iR.transactionResponse.messages[0].code;
                stcR.resultText = iR.transactionResponse.messages[0].description;
            }
            else
            {
                stcRErrors.resultCode = iR.transactionResponse.errors[0].errorCode;
                stcRErrors.resultText = iR.transactionResponse.errors[0].errorText;
            }
        }
        else
        {
            if (iR.transactionResponse != null && iR.transactionResponse.errors != null)
            {
                stcRErrors.resultCode = iR.transactionResponse.errors[0].errorCode;
                stcRErrors.resultText = iR.transactionResponse.errors[0].errorText;
            }
            else
            {
                stcR.resultCode = iR.transactionResponse.messages[0].code;
                stcR.resultText = iR.transactionResponse.messages[0].description;
            }
        }

        return sR;
    }

    protected internal stcResponseSet GetCreateSubscriptionResponse(ref stcTransactionDetailSet sT, A.Api.Contracts.V1.ARBCreateSubscriptionResponse iR)
    {
        stcResponseSet sR = sT.transaction.response;
        if (sR.responseErrorCode.errorMessage == null)
            sR = new stcResponseSet()
            {
                responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.No_Error, errorMessage = "" }
            };

        stcResultSet stcR = new stcResultSet();

        if (iR.messages.resultCode == messageTypeEnum.Ok)
        {
            sR.referenceId = iR.refId;
            sR.stcSubscription.subscriptionId = iR.subscriptionId;
            sR.stcSubscription.customerProfileId = iR.profile.customerProfileId;
            sR.stcSubscription.customerPaymentProfileId = iR.profile.customerPaymentProfileId;
            sR.stcSubscription.customerAddressId = iR.profile.customerAddressId;

            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }
        else
        {
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }

        return sR;
    }

    protected internal stcResponseSet GetUpdateSubscriptionResponse(ref stcTransactionDetailSet sT, A.Api.Contracts.V1.ARBUpdateSubscriptionResponse iR)
    {
        stcResponseSet sR = sT.transaction.response;
        if (sR.responseErrorCode.errorMessage == null)
            sR = new stcResponseSet()
            {
                responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.No_Error, errorMessage = "" }
            };

        stcResultSet stcR = new stcResultSet();

        if (iR.messages.resultCode == messageTypeEnum.Ok)
        {
            sR.referenceId = iR.refId;
            sR.stcSubscription.customerProfileId = iR.profile.customerProfileId;
            sR.stcSubscription.customerPaymentProfileId = iR.profile.customerPaymentProfileId;
            sR.stcSubscription.customerAddressId = iR.profile.customerAddressId;

            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }
        else
        {
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }

        return sR;
    }

    protected internal stcResponseSet GetCancelSubscriptionResponse(ref stcTransactionDetailSet sT, A.Api.Contracts.V1.ARBCancelSubscriptionResponse iR)
    {
        stcResponseSet sR = sT.transaction.response;
        if (sR.responseErrorCode.errorMessage == null)
            sR = new stcResponseSet()
            {
                responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.No_Error, errorMessage = "" }
            };

        stcResultSet stcR = new stcResultSet();

        if (iR.messages.resultCode == messageTypeEnum.Ok)
        {
            sR.referenceId = iR.refId;
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }
        else
        {
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }

        return sR;
    }

    protected internal stcResponseSet GetTransactionListResponse(ref stcTransactionDetailSet sT, A.Api.Contracts.V1.getTransactionListResponse iR)
    {
        stcResponseSet sR = sT.transaction.response;
        if (sR.responseErrorCode.errorMessage == null)
            sR = new stcResponseSet()
            {
                responseErrorCode = new stcErrorCodeSet() { errorCode = enErrorCodeList.No_Error, errorMessage = "" }
            };

        stcResultSet stcR = new stcResultSet();

        if (iR.messages.resultCode == messageTypeEnum.Ok)
        {
            sR.referenceId = iR.refId;

            if (iR.transactions != null)
            {
                sR.transactionNumber = iR.transactions[0].transId;
                sR.transactionStatus = iR.transactions[0].transactionStatus;
                sR.billing.firstName = iR.transactions[0].firstName;
                sR.billing.lastName = iR.transactions[0].lastName;

                int count = 0;

                foreach (enAccountTypeList AVSitemList in Enum.GetValues(typeof(enAccountTypeList)))
                {
                    if (string.Equals(AVSitemList.ToString(), iR.transactions[0].accountType, StringComparison.CurrentCultureIgnoreCase))
                    {
                        sR.accountType = ((Commercc.Transaction.Common.enAccountTypeList[])Enum.GetValues(typeof(enAccountTypeList)))[count];
                        count = 0;
                        break;
                    }
                    count = count + 1;
                }

                //sR.accountType = (enAccountTypeList)Enum.Parse(typeof(enAccountTypeList), iR.transactions[0].accountType);
                sR.accountNumber = iR.transactions[0].accountNumber;

                if (iR.transactions[0].subscription != null)
                {
                    sR.stcSubscription.subscriptionId = Convert.ToString(iR.transactions[0].subscription.id);
                }
                if (iR.transactions[0].profile != null)
                {
                    sR.stcSubscription.customerProfileId = iR.transactions[0].profile.customerProfileId;
                    sR.stcSubscription.customerPaymentProfileId = iR.transactions[0].profile.customerPaymentProfileId;
                }
            }
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }
        else
        {
            stcR.resultCode = iR.messages.message[0].code;
            stcR.resultText = iR.messages.message[0].text;
        }

        return sR;
    }
}