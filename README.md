# 8086

## Build & Usage

```shell
# Debug
dotnet build

# Release
dotnet publish --configuration Release

# Run with binary files from data folder
./8086.exe < listing_0039_more_movs

8086 v0.1

bits 16

0x0000 | 89 88 -- -- -- -- | mov si, bx
0x0002 | 88 b1 -- -- -- -- | mov dh, al
0x0004 | b1 0c -- -- -- -- | mov cl, 12
0x0006 | b5 f4 -- -- -- -- | mov ch, -12
0x0008 | b9 0c 00 -- -- -- | mov cx, 12
0x000b | b9 f4 ff -- -- -- | mov cx, -12
0x000e | ba 6c 0f -- -- -- | mov dx, 3948
0x0011 | ba 94 f0 -- -- -- | mov dx, -3948
0x0014 | 8a 8b -- -- -- -- | mov al, [bx + si]
0x0016 | 8b 8b -- -- -- -- | mov bx, [bp + di]
0x0018 | 8b 00 -- -- -- -- | mov dx, [bp]
0x001b | 8a 04 -- -- -- -- | mov ah, [bx + si + 4]
0x001e | 8a 87 13 -- -- -- | mov al, [bx + si + 4999]
0x0022 | 89 88 -- -- -- -- | mov [bx + di], cx
0x0024 | 88 88 -- -- -- -- | mov [bp + si], cl
0x0026 | 88 00 -- -- -- -- | mov [bp], ch
```
