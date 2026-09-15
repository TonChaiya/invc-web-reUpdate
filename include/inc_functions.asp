<%

Function YearBudget(Number)

Dim A, B 
A = Month([Number])
B = Year([Number])+543
If A = "10" Then
	YearBudget = B + 1
ElseIf A = "11" Then
	YearBudget = B + 1
ElseIf A = "12" Then
	YearBudget = B + 1
Else
	YearBudget = B
End If

End Function


Function CMonth(Number)

Dim D, M, Y 
D = Day([Number])
M = Month([Number])
Y = Year([Number])+543

if M < 9 then 
	M = "0" & M 
end if

CMonth = Y & M 

end function 




%>

