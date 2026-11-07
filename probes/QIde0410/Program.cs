// flag-variable exit
bool found = false;
for (int i = 0; i < 3 && !found; i++)
{
    for (int j = 0; j < 3; j++)
    {
        if (i * j == 2) { found = true; break; }
    }
}
// goto exit
for (int i = 0; i < 3; i++)
{
    for (int j = 0; j < 3; j++)
    {
        if (i * j == 2) goto END;
    }
}
END:
// goto next
for (int i = 0; i < 3; i++)
{
    for (int j = 0; j < 3; j++)
    {
        if (j == 1) goto NEXT;
        Console.WriteLine(j);
    }
NEXT: ;
}
Console.WriteLine(found);
