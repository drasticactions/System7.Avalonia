        org 0
entry:
        movem.l d0-d7/a0-a6,-(sp)
        lea initialized(pc),a0
        tst.w (a0)
        bne restored
        move.w #1,(a0)
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,a4
        clr.w -(sp)
        move.l a4,-(sp)
        ifd DEPTH32
        move.w #32,-(sp)
        else
        move.w #16,-(sp)
        endif
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        addq.l #2,sp
restored:
        movem.l (sp)+,d0-d7/a0-a6
        dc.w $4e56,$ffe6
        bra.w restored
initialized:
        dc.w 0
