![NGL_Logo](https://github.com/abalu/NeoSmartAddress/blob/master/img/ngl_logo.PNG)

# NEO GAS Liquid (NGL)
This is a Smart Contract for lending GAS.

Actual status: fast prototype – not ready for production environment (see future work). 

# UPDATE
Since transfering global assets into Smart-Contracts have been limited, this concept has to be rethinked:
"However, due to the limitations and security considerations of NEO smart contracts, it is not possible to transfer global assets (such as NEO or GAS) into smart contracts." See: https://medium.com/neo-smart-economy/15-things-you-should-know-about-cneo-and-cgas-1029770d76e0
I am working on how the purpose of this Smart-Contract can be achieved with CNEO and CGAS. 

# Preface
NEO is the governing Coin of the NEO Smart Economy. For staking NEO in a personal Wallet you will receive GAS as a reward. GAS is the Token that powers the NEO Blockchain, the Utility-Token. Users of the Blockchain pay fees in GAS to deploy and run NEO Smart Contracts, since the computing resources consumed by the contract need to be paid.  GAS is generated at a rate of 8 GAS per Block of the NEO Blockchain and is equally distributed to the NEO holders. The rate of production is reduced by 1 token for every 2 million blocks generated. Currently about two Blocks in a minute are generated.   

# Usage
The NEO GAS Liquid smart contract is a secured loan contract.  The GAS borrower pledges an amount of NEO into the contract in exchange for some amount of GAS that they want to borrow from a creditor. The claimable GAS reward assigned to the given NEO serves as collateral for the loan, which becomes a secured debt owed to the creditor who gives the loan. The claimable GAS reward is equal to the borrowed amount plus an agreed-upon amount of interest. As soon as the GAS reward has been generated using the pledged NEO, the assets are returned to the partaking stakeholders (NEO to the borrower, GAS reward to the creditor) and the agreement is fulfilled.   
This Smart Contract supports the desire of NEO supporters and investors who want to hold and keep their NEO Assets, but want to take part in funding new projects using their future GAS reward. This solution can only be built upon the NEO Smart Economy, due to it’s unique two token concept. It provides beneficial advantages by design over all other Blockchains.     
 
The following Operations are implemented in this dApp:


createLoanOffer(address of offered GAS, amout of offered GAS, interest, duration as block height)

This allows users who want to lend GAS to declare an offer for a GAS Loan on the NGL smart contract.
For instance, createLoanOffer(addressHash, 8, 10, 3963966) would offer to loan 8 GAS, with an interest of 10% until block height3963966 is reached, resulting in a claimable reward of 8 x 0.10 = 8.8 GAS and thus a profit of 0.8 GAS for the creditor.


createLoanDemand(address of offered NEO, neoToPledge, interest, duration as block height)

This allows users to declare a demand for a GAS Loan on the NGL smart contract.
For instance, createLoanDemand(addressHash, 200, 15, 15, 20691196) would offer to borrow 8 GAS, with a pledge of 4 NEO and interest of 0.15, resulting in a claimable reward of 8 x 1.15 = 9.2 GAS and thus a profit of 1.2 GAS for the creditor.


fillOfferLoan (demandAddress, loanOffer)

This allows the users to accept a loan offer that is listed on the smart contract. The user has to send in enough NEO to succeed with this call. 


fillDemandLoan(offerAddress, loanDamand)

This allows the users to accept a loan demand. The user has to send in the demanded amount of GAS to. 


claimClosingLoan(loan)

This allows one of the user to claim for the closing of an agreed Loan. This operation will only succeed after the agreed block height is reached an will transfer GAS + interest to the creditor and the pledged NEO to the borrower.  

# Use Cases
The NEO GAS Liquid smart contract enables lending to take place in a peer-to-peer manner, using the built-in GAS generation capabilities of the NEO blockchain. Unlike traditional lending schemes such as those offered by exchanges, no mediating party is involved, thus eliminating many headaches such as needing to prove one’s identity, sign pledge agreements, etc. Ease of use is also enhanced by the fact that borrowed GAS is sent directly to the borrower’s wallet rather than residing in an exchange wallet and then needing to be transferred out. Additionally, because the agreement is made in the form of a smart contract, both borrower and creditor can trust that the conditions agreed upon will be fulfilled, without having to worry about the trustworthiness of the mediating party.
There are limitless scenarios in which an easy way to borrow GAS is useful. For instance, if a new ICO is happening on the NEO blockchain and a NEO-holding user would like to take part but is currently low on GAS, they can quickly and easily pledge some of their NEO in order to borrow GAS from another user. By agreeing to pay the creditor back with interest once enough GAS has been generated, the borrower is able to participate in time-limited events that they would otherwise miss out on.


# Example Calculation

The borrower pledges 200 NEO. 
His loan demand parameters could look like:

Pledge NEO amount: 200

Demand GAS amount: 15 GAS

Interest: 2.2 GAS == 14,6 %

Maximal duration: approx. 1 year (for now the exact Block height has to be calculated)

Here you can try some example calculations:  https://neodepot.org/

# Future Work

Finish MVP

Enable Cancle Demand and Offer

Provide the possibility to pay back GAS sooner

Provide a GAS NEO Ratio, so that Loans can be paid back with Pledged NEO too


