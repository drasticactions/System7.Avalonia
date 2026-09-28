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
        clr.l -(sp)
        move.w #129,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a9bd
        move.l (sp)+,4(a4)
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a9bd
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
        move.l d0,-(sp)
        move.l d0,-(sp)
        dc.w $a873
        move.l (sp),a0
        pea 16(a0)
        dc.w $a8a3
        dc.w $a904
        rts
desktop:
        dc.w 20,0,342,512
limits:
        dc.w 80,120,300,460
        dc.b 'S7WINDOW'
state:
        ds.b 36
