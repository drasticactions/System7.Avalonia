        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        ifd COLOR
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,38(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 38(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a14,d0
        dc.w $aaa2
        tst.w (sp)+
        bne set_depth
        lsr.w #1,d5
        bne find_depth
        bra depth_ready
set_depth:
        clr.w -(sp)
        move.l 38(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        addq.l #2,sp
depth_ready:
        endif
        ifd WINDOW_BACKDROP
        clr.l -(sp)
        move.w #141,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        move.l (sp)+,42(a4)
        endif
        clr.l -(sp)
        move.w #140,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        move.l (sp)+,4(a4)
        move.w #128,36(a4)
create:
        clr.l -(sp)
        move.w 36(a4),-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        move.l (sp)+,(a4)
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 8(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 8(a4),d0
        cmp.w #6,d0
        beq update
        cmp.w #8,d0
        beq activate
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        clr.w -(sp)
        move.l 18(a4),-(sp)
        pea 24(a4)
        dc.w $a92c
        move.w (sp)+,28(a4)
        move.w 28(a4),d0
        cmp.w #3,d0
        beq select
        cmp.w #4,d0
        beq drag
        cmp.w #5,d0
        beq grow
        cmp.w #6,d0
        beq close
        cmp.w #7,d0
        beq zoom
        cmp.w #8,d0
        beq zoom
        bra loop
key:
        move.b 13(a4),d0
        cmp.b #'p',d0
        beq hit_test
        cmp.b #'n',d0
        beq compact_size
        cmp.b #'m',d0
        beq tiny_size
        cmp.b #'t',d0
        beq change_title
        cmp.b #'r',d0
        beq recreate
        sub.b #'1',d0
        bmi normal_key
        cmp.b #8,d0
        bgt normal_key
        and.w #$ff,d0
        add.w #128,d0
        move.w d0,36(a4)
recreate:
        move.l (a4),-(sp)
        dc.w $a914
        bra create
compact_size:
        move.w #100,d0
        move.w #60,d1
        bra size_window
tiny_size:
        move.w #24,d0
        move.w #8,d1
size_window:
        move.l (a4),-(sp)
        move.w d0,-(sp)
        move.w d1,-(sp)
        move.w #$100,-(sp)
        dc.w $a91d
        move.l (a4),d0
        bsr draw
        bra loop
change_title:
        move.l (a4),-(sp)
        pea changed_title(pc)
        dc.w $a91a
        bra loop
hit_test:
        move.l (a4),-(sp)
        dc.w $a873
        pea 18(a4)
        dc.w $a972
        pea 18(a4)
        dc.w $a870
        clr.w -(sp)
        move.l 18(a4),-(sp)
        pea 24(a4)
        dc.w $a92c
        move.w (sp)+,28(a4)
        bra loop
normal_key:
        cmp.b #'h',13(a4)
        beq hide
        cmp.b #'s',13(a4)
        bne loop
        move.l (a4),-(sp)
        dc.w $a915
        move.l (a4),-(sp)
        dc.w $a91f
        bra loop
hide:
        move.l (a4),-(sp)
        dc.w $a916
        bra loop
select:
        move.l 24(a4),-(sp)
        dc.w $a91f
        bra loop
drag:
        btst #0,22(a4)
        bne drag_track
        move.l 24(a4),-(sp)
        dc.w $a91f
drag_track:
        move.l 24(a4),-(sp)
        move.l 18(a4),-(sp)
        pea desktop(pc)
        dc.w $a925
        bra loop
grow:
        clr.l -(sp)
        move.l 24(a4),-(sp)
        move.l 18(a4),-(sp)
        pea limits(pc)
        dc.w $a92b
        move.l (sp)+,d0
        beq loop
        move.l d0,32(a4)
        move.l 24(a4),-(sp)
        move.w d0,-(sp)
        swap d0
        move.w d0,-(sp)
        move.w #$100,-(sp)
        dc.w $a91d
        move.l 24(a4),d0
        bsr draw
        bra loop
close:
        clr.w -(sp)
        move.l 24(a4),-(sp)
        move.l 18(a4),-(sp)
        dc.w $a91e
        tst.b (sp)+
        beq loop
        addq.w #1,30(a4)
        bra loop
zoom:
        clr.w -(sp)
        move.l 24(a4),-(sp)
        move.l 18(a4),-(sp)
        move.w 28(a4),-(sp)
        dc.w $a83b
        tst.b (sp)+
        beq loop
        move.l 24(a4),-(sp)
        move.w 28(a4),-(sp)
        move.w #$100,-(sp)
        dc.w $a83a
        move.l 24(a4),d0
        bsr draw
        bra loop
update:
        move.l 10(a4),-(sp)
        dc.w $a922
        move.l 10(a4),d0
        bsr draw
        move.l 10(a4),-(sp)
        dc.w $a923
        bra loop
activate:
        move.l 10(a4),d0
        bsr draw
        bra loop
draw:
        ifd WINDOW_BACKDROP
        cmp.l 42(a4),d0
        beq draw_backdrop
        endif
        move.l d0,-(sp)
        move.l d0,-(sp)
        dc.w $a873
        move.l (sp),a0
        pea 16(a0)
        dc.w $a8a3
        move.l (sp),d0
        cmp.l (a4),d0
        bne grow_icon
        cmp.w #133,36(a4)
        beq grow_icon
        cmp.w #135,36(a4)
        bne skip_icon
grow_icon:
        dc.w $a904
        rts
skip_icon:
        addq.l #4,sp
        rts
        ifd WINDOW_BACKDROP
draw_backdrop:
        move.l d0,-(sp)
        dc.w $a873
        ifd COLOR
        pea backdrop_foreground(pc)
        dc.w $aa14
        pea backdrop_background(pc)
        dc.w $aa15
        endif
        move.l 42(a4),a0
        pea 16(a0)
        pea backdrop_pattern(pc)
        dc.w $a8a5
        ifd ROUND_WINDOWS
        ; WDEF 1's content region can extend beyond its structure region at tiny sizes.
        move.l (a4),d0
        bne draw
        endif
        rts
backdrop_pattern:
        dc.w $aa55,$aa55,$aa55,$aa55
backdrop_foreground:
        dc.w $6666,$6666,$6666
backdrop_background:
        dc.w $aaaa,$aaaa,$aaaa
        endif
desktop:
        dc.w 20,0,342,512
limits:
        dc.w 80,120,300,460
changed_title:
        dc.b 7,'Changed'
        dc.b 'S7WTYPES'
state:
        ds.b 42
        ifd WINDOW_BACKDROP
        ds.b 4
        endif
