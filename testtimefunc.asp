<%@LANGUAGE="VBSCRIPT" CODEPAGE="874"%>

<%
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


response.write(TimeHHMM(now))

%>