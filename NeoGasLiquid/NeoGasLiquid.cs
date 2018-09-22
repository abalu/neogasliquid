using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;


namespace neoGasLiquid
{
    public class NeoGasLiquid : SmartContract
    {

        //Admin Hash
        private static readonly byte[] Admin = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash(); //TODO: Fill in RealHash

        // Assets
        private static readonly byte[] NEO = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b".ToScriptHash(); //Used to secure NEO AssetId
        private static readonly byte[] GAS = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7".ToScriptHash(); //Used to secure GAS AssetId

        //Fee constants
        private const int feeTotal = 100; // used to calc the total fee amount
        private const int feeLimit = 50; // 50% of LoanAmount

        /* Contract States 
         * A state of the SC is put to the Neo.Storage
         * The Owner is able to turn the SC into inactive state
         */
        private static readonly byte[] Initial = { };         // initialize is the only callable operation
        private static readonly byte[] Active = { 0x01 };     // all operations open
        private static readonly byte[] Inactive = { 0x02 };   // suspended - only limited actions

        //Constants
        private static readonly byte[] Empty = { };
        private static readonly byte[] Withdrawing = { 0x50 };

        private static StorageContext Context() => Storage.CurrentContext;

        private static byte[] StoreKey(byte[] originator, byte[] assetID) => originator.Concat(assetID);
        private static byte[] WithdrawalKey(byte[] originator) => originator.Concat(Withdrawing);

        //Contract Address 
        //ExecutionEngine.ExecutingScriptHash

        // Loan Types
        private static readonly byte[] LoanOffer = { 0x99 };
        private static readonly byte[] LoanDemand = { 0x98 };


        //The GAS loan 
        private struct Loan
        {
            public byte[] LoanType;          //a GAS loan can be offered or demanded
            public byte[] LoanAddress;       //case offer: address of the offered GAS, case demand: address of the NEO pledge
            public BigInteger LoanAmount;    //amount of offered GAS (principal) or amount of NEO (collateral) 
            public byte[] LoanInterest;  //interest rate in % of offered GAS (cost)  
            public BigInteger LoanDuration;  //Block height 
        }

        //Loan Offer
        private static Loan NewLoanOffer(
           byte[] loanOfferAddress,
           byte[] loanOfferAmount,
           byte[] loanOfferInterest,
           byte[] loanOfferDuration
           )
        {
            var loanType = LoanOffer;
            return new Loan
            {
                LoanType = loanType,
                LoanAddress = loanOfferAddress,
                LoanAmount = loanOfferAmount.AsBigInteger(),
                LoanInterest = loanOfferInterest,
                LoanDuration = loanOfferDuration.AsBigInteger()
            };
        }

        //Loan Demand - for now this is the same as LoanOffer, but it will probably change in future
        private static Loan NewLoanDemand(
            byte[] loanDemandAddress,
            byte[] loanDemandAmount,
            byte[] loanDemandInterest,
            byte[] loanDemandDuration
            )
        {
            var loanType = LoanDemand;
            return new Loan
            {
                LoanType = loanType,
                LoanAddress = loanDemandAddress,
                LoanAmount = loanDemandAmount.AsBigInteger(),
                LoanInterest = loanDemandInterest,
                LoanDuration = loanDemandDuration.AsBigInteger()
            };
        }


        [DisplayName("createdOffer")]
        public static event Action<byte[], BigInteger, byte[], BigInteger, byte[]> CreatedOffer; //loanOfferAddress, loanOfferAmount, loanOfferInterest, loanOfferDuration, loanOfferID

        [DisplayName("createdDemand")]
        public static event Action<byte[], BigInteger, byte[], BigInteger, byte[]> CreatedDemand; //loanDemandAddress, loanDemandAmount, loanDemandInterest, loanDemandDuration. loanDemandID

        // [DisplayName("filledOffer")]
        // public static event Action<byte[], byte[]> FilledOffer; //demandAddress, loanOffer

        //[DisplayName("filledDemand")]
        // public static event Action<byte[], byte[]> FilledDemand; //offerAddress, loanDemand

        //[DisplayName("cancledOffer")]
        //public static event Action<byte[], byte[]> CancledOffer; //offerAddress, loanOffer

        //[DisplayName("cancledDemand")]
        //public static event Action<byte[], byte[]> CancledDemand; //offerAddress, loanDemand

        //[DisplayName("closingClaimed")]
        //public static event Action<byte[]> ClosingClaimed; // TODO: add in all info 



        /// <summary>
        /// NEOGASLiquid
        /// This is the Smart-Contract invocation point
        /// In the smart contract deployment or invocation, you need to specify the parameters of the smart contract.
        /// Smart contract parameters are byte types.
        /// You can find the full definition of the byte types at docs.noe.org
        /// Use 2 hexadecimal characters for each parameter.
        /// 
        /// Params: 0710, return: 05
        /// </summary>
        /// 
        /// <param name="operation">
        ///  The name of the invoking operation.
        /// </param>
        /// 
        /// <param name="args">
        ///  Input arguments for the invoked operation.
        /// </param>

        public static object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification) //Verification Set   
            {
                // ContractTransaction received

                //check if contract is initialized
                if (GetState() == Initial) return false;

                //Withdraw asset
                //get the transaction
                var transaction = (Transaction)ExecutionEngine.ScriptContainer;
                if (!IsWithdrawingAsset(transaction)) return false;

                // Get the withdrawal destination address
                var destinationAddress = GetTransactionDestinationAddress(transaction);

                // Verify the outputs 
                var transactionOutputs = transaction.GetOutputs();
                ulong countOut = 0;
                foreach (var output in transactionOutputs)
                {
                    // Get amount for each asset
                    var amount = GetAmountForAssetInOutputs(output.AssetId, transactionOutputs);
                    // Verify that the output address owns the balance 
                    if (!VerifyWithdrawal(destinationAddress, output.AssetId, amount)) return false;
                    // Accumulate total for checking against inputs later
                    countOut += (ulong)output.Value;
                }


                var startOfWithdrawal = (uint)Storage.Get(Context(), WithdrawalKey(destinationAddress)).AsBigInteger();
                var currentHeight = Blockchain.GetHeight();
                if (startOfWithdrawal == 0) return false;

                // Check that withdrawal was not already done
                for (var i = startOfWithdrawal; i < currentHeight; i++)
                {
                    var block = Blockchain.GetBlock(i);
                    var transactions = block.GetTransactions();
                    foreach (var t in transactions)
                    {
                        // double spending
                        if (IsWithdrawingAsset(t) &&
                            GetTransactionDestinationAddress(t) == destinationAddress) return false;
                    }
                }

                // Ensure that nothing is burnt
                ulong countIn = 0;
                foreach (var i in transaction.GetReferences()) countIn += (ulong)i.Value;
                if (countIn != countOut) return false;

                return true;
            }
            else if (Runtime.Trigger == TriggerType.Application) //Application Set
            {
                // InvocationTransaction received
                // *** Initialization ***
                if (operation == "initialize")
                {
                    if (!Runtime.CheckWitness(Admin))
                    {
                        Runtime.Log("Owner verification failed");
                        return false;
                    }
                    if (args.Length != 2) return false;
                    return Initialize((BigInteger)args[0], (byte[])args[1]); //Init call with feeAmount & Fee-Address
                }


                // *** info getters set ***
                if (operation == "getState") return GetState();
                if (operation == "getFee") return GetFee();
                if (operation == "getFeeAddress") return GetFeeAddress();


                // *** Execution ***
                //Create LoanOffer 
                if (operation == "createLoanOffer")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 4) return false;
                    var loanOffer = NewLoanOffer((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3]);
                    return CreateLoanOffer(loanOffer);
                }

                //Create LoanDemand
                if (operation == "createLoanDemand")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 4) return false;
                    var loanDemand = NewLoanDemand((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3]);
                    return CreateLoanDemand(loanDemand);
                }

                //fillOfferLoan
                if (operation == "fillOfferLoan")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 2) return false;
                    return FillOfferLoan((byte[])args[0], (byte[])args[1]);
                }

                //fillDemandLoan
                if (operation == "fillDemandLoan")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 2) return false;
                    return FillDemandLoan((byte[])args[0], (byte[])args[1]);
                }


                //claimClosingLoan
                if (operation == "claimClosingLoan")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 1) return false;
                    return ClaimClosingLoan((byte[])args[0]);
                }


                ////cancelLoanOffer
                //if (operation == "cancelLoanOffer")
                //{
                //    if (GetState() != Active) return false;
                //    if (args.Length != 1) return false;
                //    return CancelLoanOffer((byte[])args[0]);
                //}

                ////cancelLoanDemand
                //if (operation == "cancelLoanDemand")
                //{
                //    if (GetState() != Active) return false;
                //    if (args.Length != 1) return false;
                //    return CancelLoanDemand((byte[])args[0]);
                //}


                // *** Admin Operations ***
                if (!Runtime.CheckWitness(Admin))
                {
                    Runtime.Log("Owner verification failed");
                    return false;
                }
                if (operation == "suspend")
                {
                    Storage.Put(Context(), "state", Inactive);
                    return true;
                }
                if (operation == "continue")
                {
                    Storage.Put(Context(), "state", Active);
                    return true;
                }
                if (operation == "setFee")
                {
                    if (args.Length != 2) return false;
                    return SetFee((BigInteger)args[0]);
                }
                if (operation == "setFeeAddress")
                {
                    if (args.Length != 1) return false;
                    return SetFeeAddress((byte[])args[0]);
                }

            }//END Application Set

            return true;
        }


        //Initialize 
        //Pass through the fee amount for Loan Agreements and the address where the fees should go to
        private static bool Initialize(BigInteger fee, byte[] feeAddress)
        {
            if (GetState() != Initial) return false;
            if (!SetFee(fee)) return false;
            if (!SetFeeAddress(feeAddress)) return false;

            Storage.Put(Context(), "state", Active);

            Runtime.Log("NGL initialized");
            return true;
        }


        private static bool SetFee(BigInteger fee)
        {
            if (fee > feeLimit) return false;
            if (fee < 0) return false;
            Storage.Put(Context(), "fee", fee); //keep it simple
            return true;
        }

        private static bool SetFeeAddress(byte[] feeAddress)
        {
            if (!Runtime.CheckWitness(Admin)) //probably double check
            {
                Runtime.Log("Owner verification failed");
                return false;
            }
            Storage.Put(Context(), "feeAddress", feeAddress);
            return true;
        }

        private static byte[] GetState()
        {
            return Storage.Get(Context(), "state");
        }

        private static byte[] GetFee()
        {
            return Storage.Get(Context(), "fee");
        }


        private static byte[] GetFeeAddress()
        {
            return Storage.Get(Context(), "feeAddress");
        }


        private static bool CreateLoanOffer(Loan loanOffer)
        {
            // Check that the loanOffer is signed 
            if (!Runtime.CheckWitness(loanOffer.LoanAddress)) return false;

            //Check Balance - user has enough to make this offer
            //TODO: .. 

            //Check offer, interest > 0
            if (!(loanOffer.LoanAmount > 0)) return false;

            //Check duration (Block height) > actual Block height
            if (loanOffer.LoanDuration < Blockchain.GetHeight()) return false;

            // Reduce available balance for the offered asset and amount
            // TODO: ..


            //Create offerHashID
            byte[] loanOfferID = loanOffer.LoanAddress
                                            .Concat((loanOffer.LoanAmount.AsByteArray().Take(8))
                                            .Concat(loanOffer.LoanDuration.AsByteArray().Take(8))
                                            .Concat(loanOffer.LoanInterest));

            // Add the offer to storage
            Storage.Put(Context(), "loanType", loanOffer.LoanType);
            Storage.Put(Context(), "loanAddress", loanOffer.LoanAddress);
            Storage.Put(Context(), "loanAmount", loanOffer.LoanAmount);
            Storage.Put(Context(), "loanInterest", loanOffer.LoanInterest);
            Storage.Put(Context(), "loanDuration", loanOffer.LoanDuration);
            Storage.Put(Context(), "loanOfferID", loanOfferID);

            // Notify
            //loanOfferAddress, loanOfferAmount, loanOfferInterest, loanOfferDuration, loanOfferID
            CreatedOffer(loanOffer.LoanAddress, loanOffer.LoanAmount, loanOffer.LoanInterest, loanOffer.LoanDuration, loanOfferID);

            return true;
        }



        private static bool CreateLoanDemand(Loan loanDemand)
        {
            // Check that the loanOffer is signed 
            if (!Runtime.CheckWitness(loanDemand.LoanAddress)) return false;

            //Check Balance - user has enough to make this offer
            //TODO: .. 

            //Check demand, interest > 0
            if (!(loanDemand.LoanAmount > 0)) return false;

            //Check duration (Block height) > actual Block height
            if (loanDemand.LoanDuration < Blockchain.GetHeight()) return false;

            // Reduce available balance for the offered asset and amount
            // TODO: ..

            //Create demandHashID
            byte[] loanDemandID = loanDemand.LoanAddress
                                            .Concat((loanDemand.LoanAmount.AsByteArray().Take(8))
                                            .Concat(loanDemand.LoanDuration.AsByteArray().Take(8))
                                            .Concat(loanDemand.LoanInterest));


            // Add the offer to storage
            Storage.Put(Context(), "loanType", loanDemand.LoanType);
            Storage.Put(Context(), "loanAddress", loanDemand.LoanAddress);
            Storage.Put(Context(), "loanAmount", loanDemand.LoanAmount);
            Storage.Put(Context(), "loanInterest", loanDemand.LoanInterest);
            Storage.Put(Context(), "loanDuration", loanDemand.LoanDuration);
            Storage.Put(Context(), "loanDemandID", loanDemandID);

            // Notify
            //loanDemandAddress, loanDemandAmount, loanDemandInterest, loanDemandDuration, loanDemandID
            CreatedDemand(loanDemand.LoanAddress, loanDemand.LoanAmount, loanDemand.LoanInterest, loanDemand.LoanDuration, loanDemandID);
            return true;

        }


        private static bool FillOfferLoan(byte[] demandAddress, byte[] loanOfferID)
        {
            // Check that the loanOffer is signed 
            if (!Runtime.CheckWitness(demandAddress)) return false;

            //GetLoanOfferDetails
            Loan offerloan = GetLoan(loanOfferID, LoanOffer);

            //Check Balance

            //Check 


            //Delete Offer from Storage

            return true;

        }

        private static bool FillDemandLoan(byte[] offerAddress, byte[] loanDemandID)
        {
            // Check that the loanDemand is signed 
            if (!Runtime.CheckWitness(offerAddress)) return false;

            // Check                 
            //GetLoanDemandDetails
            Loan offerloan = GetLoan(loanDemandID, LoanDemand);



            //Delete Demand from Storage

            return true;

        }

        private static bool ClaimClosingLoan(byte[] sender)
        {
            // Check that the loanDemand is signed 
            if (!Runtime.CheckWitness(sender)) return false;

            // Check                 

            return true;

        }

        private static Loan GetLoan(byte[] loanID, byte[] loanType)
        {
            // Check that offer exists
            var loanData = Storage.Get(Context(), loanID);
            if (loanData == Empty) return new Loan(); // invalid offer hash

            // Deserialize Loan
            var index = 0;

            var loanAddress = loanData.Range(index, 34);
            index += 34;
            var bigIntLength = 8;

            var loanAmount = loanData.Range(index, bigIntLength);
            index += bigIntLength;

            var loanDuration = loanData.Range(index, bigIntLength);
            index += bigIntLength;

            var loanInterest = loanData.Range(index, (loanData.Length - index));

            if (loanType == LoanDemand) { return NewLoanDemand(loanAddress, loanAmount, loanInterest, loanDuration); }
            else if (loanType == LoanOffer) { return NewLoanOffer(loanAddress, loanAmount, loanInterest, loanDuration); }
            else { return new Loan(); } //sth went wrong

        }


        //Transaction.Type Property
        //Attribute value: Transaction type as a byte.
        //Invoke smart contract transactions
        //InvocationTransaction = 0xd1
        //can be used to do the data stored in Hash: Hash1 = 0xa1, Hash2 = 0xa2, Hash3 = 0xa3, ...
        private static bool IsWithdrawingAsset(Transaction transaction)
        {
            //is invocation transaction
            if (transaction.Type != 0xd1) return false;

            //is withdrawal transaction
            var transactionAttributes = transaction.GetAttributes();
            foreach (var attribute in transactionAttributes)
            {
                if (attribute.Data == ExecutionEngine.ExecutingScriptHash && attribute.Usage == 0xa1) return true; //at least one hash data stored
            }

            return false;
        }

        //TransactionAttribute.Usage Property
        //for additional verification of the transaction
        //Script = 0x20,
        private static byte[] GetTransactionDestinationAddress(Transaction transaction)
        {
            var transactionAttributes = transaction.GetAttributes();
            foreach (var attribute in transactionAttributes)
            {
                // verification script of the transaction == 0x20
                if (attribute.Usage == 0x20) return attribute.Data;
            }
            return Empty;
        }

        private static ulong GetAmountForAssetInOutputs(byte[] assetID, TransactionOutput[] outputs)
        {
            ulong amount = 0;
            foreach (var output in outputs)
            {
                if (output.AssetId == assetID && output.ScriptHash != ExecutionEngine.ExecutingScriptHash) amount += (ulong)output.Value;
            }

            return amount;
        }

        private static bool VerifyWithdrawal(byte[] holderAddress, byte[] assetID, BigInteger amount)
        {
            var balance = GetBalance(holderAddress, assetID);
            if (balance < amount) return false;
            return true;
        }

        private static BigInteger GetBalance(byte[] address, byte[] assetID)
        {
            return Storage.Get(Context(), StoreKey(address, assetID)).AsBigInteger();
        }

    }
}
