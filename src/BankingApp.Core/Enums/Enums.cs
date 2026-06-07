namespace BankingApp.Core.Enums;

// stored as integers in the database instead of strings — more efficient and harder to misspell
// checking = standard current account, savings = higher interest, credit = credit card
public enum AccountType { Checking = 0, Savings = 1, Credit = 2 }

// income adds to balance, expense deducts, transfer moves between accounts
public enum TransactionType { Income = 0, Expense = 1, Transfer = 2 }

// used for categorizing transactions in reports and the category breakdown chart
public enum TransactionCategory
{
    Salary = 0,
    Food = 1,
    Transport = 2,
    Utilities = 3,
    Healthcare = 4,
    Entertainment = 5,
    Shopping = 6,
    Education = 7,
    Housing = 8,
    Insurance = 9,
    Savings = 10,
    Other = 11
}

// controls how often the background worker executes a recurring payment
public enum RecurringFrequency { Daily = 0, Weekly = 1, Monthly = 2, Yearly = 3 }

// pending = not yet paid, paid = payment made, overdue = past due date, cancelled = no longer needed
public enum BillStatus { Pending = 0, Paid = 1, Overdue = 2, Cancelled = 3 }

// used for categorizing bills in the bills page
public enum BillCategory
{
    Electricity = 0,
    Water = 1,
    Gas = 2,
    Internet = 3,
    Phone = 4,
    Rent = 5,
    Insurance = 6,
    Subscription = 7,
    Other = 8
}