<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<!--#include file="Connections/INVFlood.asp" -->
<!--#include file="Connections/EPOC.asp" -->

<%

setlocale(1054) 

Function Y543(Number)
Dim A, B, C, D, E, F, G
A = formatdatetime(Number, 2)
B = Right(A, 4) '-543
C = Left(A, len(A)-4)
D =  left(right(C,len(C) -instr(1,C,"/")),len(right(C,len(C) -instr(1,C,"/")))-1)
E = left(C,instr(1,C,"/")-1)
if len(D) = 1 then 
	D = "0" & D 
end if 
if len(E) = 1 then 
	E = "0" & E 
end if 
'Y543 = B - 543 & "/" & d & "/" & e  
Y543 = B & D & E

End Function


Function TimeHHMM(Number)

Dim A, B, C
A = formatdatetime(Number, 4)
B = left(A, 2) '-543
C = right(A, 2)

if len(B) = 1 then 
	B = "0" & B 
end if 
if len(C) = 1 then 
	C = "0" & C 
end if 
'Y543 = B - 543 & "/" & d & "/" & e  
TimeHHMM = B & C

End Function

PO_NO = request.QueryString("PO_NO")
'PO_NO = "6700080"

Set PO = Server.CreateObject("ADODB.Recordset")
PO.ActiveConnection = MM_INVFlood_STRING
PO.Source = "SELECT m.PO_NO, m.TOTAL_COST, c.COMPANY_NAME_PO, o.[name],d.DRUG_NAME FROM MS_PO m INNER JOIN MS_PO_C p ON m.PO_NO=p.PO_NO INNER JOIN COMPANY c on m.VENDOR_CODE=c.COMPANY_CODE INNER JOIN TBLCOMM o on m.COM6=o.[Code] INNER JOIN INV_MD d on p.WORKING_CODE=d.WORKING_CODE WHERE m.PO_NO='" & PO_NO & "'"
PO.CursorType = 2
PO.CursorLocation = 2
PO.LockType = 3
PO.Open()

If not PO.eof then

while not PO.eof 
	DrugName = DrugName & PO("DRUG_NAME") & "," 
PO.movenext
wend
DrugName = left(DrugName,len(DrugName)-1) 
	
PO.Movefirst
'response.write (PO("PO_NO"))
EPOC_YEAR = "25" & left(PO("PO_NO"),2)

end if

'EPOC_YEAR = 2567
'response.write (EPOC_YEAR)
'response.write ("SELECT * FROM bookPO WHERE bookYear=" & EPOC_YEAR & " ORDER BY bookNo desc" )

Set EPOC = Server.CreateObject("ADODB.Recordset")
EPOC.ActiveConnection = MM_EPOC_STRING
'EPOC.Source = "SELECT * FROM bookPO WHERE bookNo=0 AND bookYear=0"
EPOC.Source = "SELECT * FROM bookPO WHERE bookYear=" & EPOC_YEAR & " ORDER BY bookNo desc" 
EPOC.CursorType = 2
EPOC.CursorLocation = 2
EPOC.LockType = 3
EPOC.Open()


If not EPOC.eof then
		
	'response.write(EPOC("EPOC"))
	
		bookNo = cint(EPOC("bookNo")) + 1 
		response.write (right(EPOC_YEAR,2) & string(5-len(bookNo),"0") & bookNo)
		
	EPOC.addnew
		EPOC("bookNo") = bookNo
		EPOC("bookYear") = EPOC_YEAR
		EPOC("EPOC") = right(EPOC_YEAR,2) & string(5-len(bookNo),"0") & bookNo
		
		EPOC("deptCode") = "104.16.06"
		EPOC("deptName") = "งานจัดซื้อและธุรการ กลุ่มงานเภสัชกรรม"
		EPOC("bookType") = "ทั่วไป"
		EPOC("bookPO") = "รอ"
		EPOC("buyGroup") = 1
		EPOC("bookTo") = PO("COMPANY_NAME_PO")
		EPOC("bookSubject") = DrugName
		EPOC("signatory") = PO("name")
		EPOC("bookAction") = "นางสาวสุกัญญา ตุงคบุรี"
		'EPOC("bookDetail") = PO("")
		EPOC("saveBy") = "poiifay"
		EPOC("saveByName") = "สุกัญญา ตุงคบุรี"
		EPOC("saveDate") = Y543(date)
		EPOC("saveTime") = TimeHHMM(Now)
		'EPOC("editBy") = ""
		'EPOC("editDate") = PO("")
		'EPOC("editTIme") = PO("")
		EPOC("budget") = PO("TOTAL_COST")
		EPOC("cancelFlag") = "N"
		EPOC("contract") = "N"
		EPOC("installment") = 0
		
	EPOC.update
		
end if



Response.charset="windows-874"

%>
